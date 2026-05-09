#include "windowsblescanner.h"

#include "../../ble/bleadvertisementparser.h"

#include <QByteArray>
#include <QMetaObject>
#include <QMetaType>
#include <QString>
#include <QtGlobal>

#include <winrt/Windows.Devices.Bluetooth.Advertisement.h>
#include <winrt/Windows.Foundation.Collections.h>
#include <winrt/Windows.Storage.Streams.h>

using namespace winrt::Windows::Devices::Bluetooth::Advertisement;
using namespace winrt::Windows::Foundation;
using namespace winrt::Windows::Storage::Streams;

namespace
{
constexpr uint16_t AppleCompanyId = 0x004C;

QByteArray bufferToByteArray(const IBuffer &buffer)
{
    DataReader reader = DataReader::FromBuffer(buffer);
    QByteArray bytes;
    bytes.resize(static_cast<qsizetype>(buffer.Length()));
    if (!bytes.isEmpty())
    {
        reader.ReadBytes(winrt::array_view<uint8_t>(
            reinterpret_cast<uint8_t *>(bytes.data()),
            reinterpret_cast<uint8_t *>(bytes.data()) + bytes.size()));
    }
    return bytes;
}

QString formatBluetoothAddress(uint64_t address)
{
    return QStringLiteral("%1:%2:%3:%4:%5:%6")
        .arg(static_cast<quint8>((address >> 40) & 0xff), 2, 16, QLatin1Char('0'))
        .arg(static_cast<quint8>((address >> 32) & 0xff), 2, 16, QLatin1Char('0'))
        .arg(static_cast<quint8>((address >> 24) & 0xff), 2, 16, QLatin1Char('0'))
        .arg(static_cast<quint8>((address >> 16) & 0xff), 2, 16, QLatin1Char('0'))
        .arg(static_cast<quint8>((address >> 8) & 0xff), 2, 16, QLatin1Char('0'))
        .arg(static_cast<quint8>(address & 0xff), 2, 16, QLatin1Char('0'))
        .toUpper();
}
}

WindowsBleScanner::WindowsBleScanner(QObject *parent)
    : IBleScanner(parent)
{
    qRegisterMetaType<BleInfo>("BleInfo");
    winrt::init_apartment(winrt::apartment_type::multi_threaded);

    m_watcher = BluetoothLEAdvertisementWatcher();
    m_watcher.ScanningMode(BluetoothLEScanningMode::Active);

    m_receivedToken = m_watcher.Received([this](const BluetoothLEAdvertisementWatcher &,
                                                const BluetoothLEAdvertisementReceivedEventArgs &args)
    {
        onAdvertisementReceived(args);
    });
}

WindowsBleScanner::~WindowsBleScanner()
{
    stopScan();
    if (m_watcher)
    {
        m_watcher.Received(m_receivedToken);
    }
}

void WindowsBleScanner::startScan()
{
    if (m_scanning || !m_watcher)
    {
        return;
    }

    try
    {
        m_watcher.Start();
        m_scanning = true;
    }
    catch (const winrt::hresult_error &error)
    {
        emitErrorQueued(QString::fromWCharArray(error.message().c_str()));
    }
}

void WindowsBleScanner::stopScan()
{
    if (!m_scanning || !m_watcher)
    {
        return;
    }

    m_watcher.Stop();
    m_scanning = false;
}

bool WindowsBleScanner::isScanning() const
{
    return m_scanning;
}

void WindowsBleScanner::onAdvertisementReceived(const BluetoothLEAdvertisementReceivedEventArgs &args)
{
    const auto manufacturerData = args.Advertisement().ManufacturerData();
    for (const auto &section : manufacturerData)
    {
        if (section.CompanyId() != AppleCompanyId)
        {
            continue;
        }

        BleInfo info;
        if (BleAdvertisementParser::parseAppleManufacturerData(
                bufferToByteArray(section.Data()),
                QString::fromWCharArray(args.Advertisement().LocalName().c_str()),
                formatBluetoothAddress(args.BluetoothAddress()),
                &info))
        {
            emitDeviceFoundQueued(info);
        }
    }
}

void WindowsBleScanner::emitDeviceFoundQueued(const BleInfo &info)
{
    QMetaObject::invokeMethod(this, [this, info]() {
        emit deviceFound(info);
    }, Qt::QueuedConnection);
}

void WindowsBleScanner::emitErrorQueued(const QString &message)
{
    QMetaObject::invokeMethod(this, [this, message]() {
        emit errorOccurred(message);
    }, Qt::QueuedConnection);
}
