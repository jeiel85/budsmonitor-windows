#include "../ble/bleadvertisementparser.h"

#include <QByteArray>
#include <QCoreApplication>
#include <QDebug>
#include <QTextStream>

int main(int argc, char *argv[])
{
    QCoreApplication app(argc, argv);

    const QByteArray payload = QByteArray::fromHex("0719010F2002F98F000005F92D652BD0EA1CF1CDB53AA3879C55B7");

    BleInfo info;
    if (!BleAdvertisementParser::parseAppleManufacturerData(payload, QStringLiteral(""), QStringLiteral("4B:E0:41:D7:A3:41"), &info))
    {
        qCritical() << "Failed to parse AirPods BLE advertisement fixture";
        return 1;
    }

    if (info.modelName != AirpodsTrayApp::Enums::AirPodsModel::AirPods2 ||
        info.rightPodBattery != 90 ||
        info.leftPodBattery != -1 ||
        info.caseBattery != -1 ||
        !info.isRightPodInEar ||
        info.isLeftPodInEar)
    {
        qCritical() << "Parsed fixture did not match expected AirPods state";
        qCritical() << "model=" << static_cast<int>(info.modelName)
                    << "left=" << info.leftPodBattery
                    << "right=" << info.rightPodBattery
                    << "case=" << info.caseBattery
                    << "leftInEar=" << info.isLeftPodInEar
                    << "rightInEar=" << info.isRightPodInEar;
        return 1;
    }

    QTextStream(stdout) << "BLE parser smoke test passed\n";
    return 0;
}
