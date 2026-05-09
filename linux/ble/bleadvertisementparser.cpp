#include "bleadvertisementparser.h"

#include <QMap>

namespace BleAdvertisementParser
{
AirpodsTrayApp::Enums::AirPodsModel modelNameForId(quint16 modelId)
{
    using namespace AirpodsTrayApp::Enums;
    static const QMap<quint16, AirPodsModel> modelMap = {
        {0x0220, AirPodsModel::AirPods1},
        {0x0F20, AirPodsModel::AirPods2},
        {0x1320, AirPodsModel::AirPods3},
        {0x1920, AirPodsModel::AirPods4},
        {0x1B20, AirPodsModel::AirPods4ANC},
        {0x0A20, AirPodsModel::AirPodsMaxLightning},
        {0x1F20, AirPodsModel::AirPodsMaxUSBC},
        {0x0E20, AirPodsModel::AirPodsPro},
        {0x1420, AirPodsModel::AirPodsPro2Lightning},
        {0x2420, AirPodsModel::AirPodsPro2USBC}
    };

    return modelMap.value(modelId, AirPodsModel::Unknown);
}

QString colorNameForId(quint8 colorId)
{
    switch (colorId)
    {
    case 0x00:
        return "White";
    case 0x01:
        return "Black";
    case 0x02:
        return "Red";
    case 0x03:
        return "Blue";
    case 0x04:
        return "Pink";
    case 0x05:
        return "Gray";
    case 0x06:
        return "Silver";
    case 0x07:
        return "Gold";
    case 0x08:
        return "Rose Gold";
    case 0x09:
        return "Space Gray";
    case 0x0A:
        return "Dark Blue";
    case 0x0B:
        return "Light Blue";
    case 0x0C:
        return "Yellow";
    default:
        return "Unknown";
    }
}

QString connectionStateName(BleInfo::ConnectionState state)
{
    using ConnectionState = BleInfo::ConnectionState;
    switch (state)
    {
    case ConnectionState::DISCONNECTED:
        return QString("Disconnected");
    case ConnectionState::IDLE:
        return QString("Idle");
    case ConnectionState::MUSIC:
        return QString("Playing Music");
    case ConnectionState::CALL:
        return QString("On Call");
    case ConnectionState::RINGING:
        return QString("Ringing");
    case ConnectionState::HANGING_UP:
        return QString("Hanging Up");
    case ConnectionState::UNKNOWN:
    default:
        return QString("Unknown");
    }
}

bool parseAppleManufacturerData(const QByteArray &data,
                                const QString &name,
                                const QString &address,
                                BleInfo *deviceInfo)
{
    if (!deviceInfo || data.size() < 11 || static_cast<quint8>(data[0]) != 0x07)
    {
        return false;
    }

    if (static_cast<quint8>(data[2]) == 0x00)
    {
        return false;
    }

    BleInfo parsed;
    parsed.name = name.isEmpty() ? "AirPods" : name;
    parsed.address = address;
    parsed.rawData = data.size() >= 16 ? data.left(data.size() - 16) : data;
    parsed.encryptedPayload = data.size() >= 16 ? data.right(16) : QByteArray();

    parsed.modelName = modelNameForId(static_cast<quint16>(static_cast<quint8>(data[4])) |
                                      (static_cast<quint8>(data[3]) << 8));

    quint8 status = static_cast<quint8>(data[5]);
    parsed.status = status;

    quint8 podsBatteryByte = static_cast<quint8>(data[6]);
    quint8 flagsAndCaseBattery = static_cast<quint8>(data[7]);
    quint8 lidIndicator = static_cast<quint8>(data[8]);
    parsed.color = colorNameForId(static_cast<quint8>(data[9]));
    parsed.connectionState = static_cast<BleInfo::ConnectionState>(static_cast<quint8>(data[10]));

    bool primaryLeft = (status & 0x20) != 0;
    bool areValuesFlipped = !primaryLeft;
    parsed.primaryLeft = primaryLeft;

    int leftNibble = areValuesFlipped ? (podsBatteryByte >> 4) & 0x0F : podsBatteryByte & 0x0F;
    int rightNibble = areValuesFlipped ? podsBatteryByte & 0x0F : (podsBatteryByte >> 4) & 0x0F;
    int caseNibble = flagsAndCaseBattery & 0x0F;
    parsed.leftPodBattery = (leftNibble == 15) ? -1 : leftNibble * 10;
    parsed.rightPodBattery = (rightNibble == 15) ? -1 : rightNibble * 10;
    parsed.caseBattery = (caseNibble == 15) ? -1 : caseNibble * 10;

    quint8 flags = (flagsAndCaseBattery >> 4) & 0x0F;
    parsed.rightCharging = areValuesFlipped ? (flags & 0x01) != 0 : (flags & 0x02) != 0;
    parsed.leftCharging = areValuesFlipped ? (flags & 0x02) != 0 : (flags & 0x01) != 0;
    parsed.caseCharging = (flags & 0x04) != 0;

    parsed.isThisPodInTheCase = (status & 0x40) != 0;
    parsed.isOnePodInCase = (status & 0x10) != 0;
    parsed.areBothPodsInCase = (status & 0x04) != 0;

    bool xorFactor = areValuesFlipped ^ parsed.isThisPodInTheCase;
    parsed.isLeftPodInEar = xorFactor ? (status & 0x08) != 0 : (status & 0x02) != 0;
    parsed.isRightPodInEar = xorFactor ? (status & 0x02) != 0 : (status & 0x08) != 0;

    parsed.isPrimaryInEar = primaryLeft ? parsed.isLeftPodInEar : parsed.isRightPodInEar;
    parsed.isSecondaryInEar = primaryLeft ? parsed.isRightPodInEar : parsed.isLeftPodInEar;

    parsed.isLeftPodMicrophone = primaryLeft ^ parsed.isThisPodInTheCase;
    parsed.isRightPodMicrophone = !primaryLeft ^ parsed.isThisPodInTheCase;

    parsed.lidOpenCounter = lidIndicator & 0x07;
    quint8 lidState = static_cast<quint8>((lidIndicator >> 3) & 0x01);
    if (parsed.isThisPodInTheCase)
    {
        parsed.lidState = static_cast<BleInfo::LidState>(lidState);
    }

    parsed.lastSeen = QDateTime::currentDateTime();
    *deviceInfo = parsed;
    return true;
}
}
