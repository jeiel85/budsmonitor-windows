using System.IO.Compression;
using System.Text;
using System.Text.Json;
using BudsMonitor.Infrastructure.Json;
using BudsMonitor.Infrastructure.Storage;

namespace BudsMonitor.Diagnostics;

/// <summary>
/// Builds a local diagnostics ZIP under %LocalAppData%\BudsMonitor\diagnostics. Nothing is
/// uploaded or transmitted — the file simply lands on disk for the user to inspect or share.
/// Bluetooth addresses are masked unless the caller explicitly opts out, and raw advertisement
/// payloads are included only on opt-in. Runs without admin rights (writes under the profile).
/// </summary>
public sealed class DiagnosticsExportService
{
    private const int MaxLogFiles = 2;
    private const int MaxLogBytesPerFile = 512 * 1024;

    private readonly StoragePaths _paths;

    public DiagnosticsExportService(StoragePaths paths) => _paths = paths;

    /// <summary>Writes the diagnostics ZIP and returns its full path.</summary>
    public string Export(DiagnosticsInput input)
    {
        Directory.CreateDirectory(_paths.DiagnosticsDirectory);

        var stamp = input.GeneratedAt.ToLocalTime().ToString("yyyyMMdd-HHmmss");
        var zipPath = Path.Combine(_paths.DiagnosticsDirectory, $"budsmonitor-diagnostics-{stamp}.zip");
        if (File.Exists(zipPath))
        {
            File.Delete(zipPath);
        }

        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            WriteJson(archive, "environment.json", DiagnosticsEnvironment.Collect(input.AppVersion, input.GeneratedAt));
            WriteJson(archive, "settings.json", input.Settings);
            WriteJson(archive, "devices.json", input.Devices);
            WriteJson(archive, "battery-cache.json", input.BatteryCache);
            WriteJson(archive, "scanner.json", input.Scanner);
            WriteJson(archive, "provider-attempts.json", input.ProviderAttempts);
            WriteJson(archive, "advertisement-samples.json",
                input.AdvertisementSamples.Select(sample => Redact(sample, input)).ToList());
            WriteText(archive, "README.txt", BuildReadme(input));
            CopyRecentLogs(archive);
        }

        return zipPath;
    }

    /// <summary>Formats a Bluetooth address, masking the lower three octets by default.</summary>
    public static string MaskAddress(ulong address, bool mask)
    {
        var bytes = new byte[6];
        for (var i = 0; i < 6; i++)
        {
            bytes[5 - i] = (byte)(address >> (i * 8));
        }

        return mask
            ? $"{bytes[0]:X2}:{bytes[1]:X2}:{bytes[2]:X2}:**:**:**"
            : string.Join(":", bytes.Select(b => b.ToString("X2")));
    }

    private static object Redact(DiagnosticsAdvertisementSample sample, DiagnosticsInput input) => new
    {
        receivedAt = sample.ReceivedAt,
        companyId = $"0x{sample.CompanyId:X4}",
        address = MaskAddress(sample.BluetoothAddress, input.MaskBluetoothAddresses),
        dataLength = sample.DataLength,
        rssi = sample.Rssi,
        localName = sample.LocalName,
        manufacturerDataHex = input.IncludeRawPayloads && sample.ManufacturerData is not null
            ? Convert.ToHexString(sample.ManufacturerData)
            : null,
    };

    private static void WriteJson(ZipArchive archive, string entryName, object? value)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(JsonSerializer.Serialize(value, StorageJson.Options));
    }

    private static void WriteText(ZipArchive archive, string entryName, string text)
    {
        var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(text);
    }

    private void CopyRecentLogs(ZipArchive archive)
    {
        if (!Directory.Exists(_paths.LogsDirectory))
        {
            return;
        }

        var logs = new DirectoryInfo(_paths.LogsDirectory)
            .GetFiles("*.log")
            .OrderByDescending(f => f.LastWriteTimeUtc)
            .Take(MaxLogFiles);

        foreach (var log in logs)
        {
            var entry = archive.CreateEntry($"logs/{log.Name}", CompressionLevel.Optimal);
            using var stream = entry.Open();
            CopyTail(log, stream, MaxLogBytesPerFile);
        }
    }

    private static void CopyTail(FileInfo file, Stream destination, int maxBytes)
    {
        // Serilog keeps the current file open with shared access; read it the same way.
        using var source = new FileStream(
            file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        if (source.Length > maxBytes)
        {
            source.Seek(-maxBytes, SeekOrigin.End);
        }

        source.CopyTo(destination);
    }

    private static string BuildReadme(DiagnosticsInput input)
    {
        var sb = new StringBuilder();
        sb.AppendLine("BudsMonitor 진단 번들");
        sb.AppendLine("======================");
        sb.AppendLine($"생성 시각: {input.GeneratedAt:o}");
        sb.AppendLine($"앱 버전: {input.AppVersion ?? "unknown"}");
        sb.AppendLine();
        sb.AppendLine("이 번들은 로컬에서만 생성되며 자동으로 전송되지 않습니다.");
        sb.AppendLine($"Bluetooth 주소 마스킹: {(input.MaskBluetoothAddresses ? "켜짐(기본)" : "꺼짐")}");
        sb.AppendLine($"원시 광고 페이로드 포함: {(input.IncludeRawPayloads ? "켜짐" : "꺼짐(기본)")}");
        sb.AppendLine();
        sb.AppendLine("포함 파일:");
        sb.AppendLine(" - environment.json         : OS/런타임 정보 (기기명·사용자명 제외)");
        sb.AppendLine(" - settings.json            : 설정 (네트워크/분석 항상 비활성)");
        sb.AppendLine(" - devices.json             : 기기 레지스트리 (기기 키는 SHA-256 해시)");
        sb.AppendLine(" - battery-cache.json       : 마지막으로 알려진 배터리 값");
        sb.AppendLine(" - scanner.json             : BLE 스캐너/라디오 상태");
        sb.AppendLine(" - provider-attempts.json   : provider 읽기 시도와 실패 사유");
        sb.AppendLine(" - advertisement-samples.json : 최근 BLE 광고 요약 (주소 마스킹)");
        sb.AppendLine(" - logs/                    : 최근 로그 파일 (최대 2개, 각 512KB 이하)");
        return sb.ToString();
    }
}
