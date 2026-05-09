#ifndef BLEADVERTISEMENTPARSER_H
#define BLEADVERTISEMENTPARSER_H

#include <QByteArray>
#include <QString>
#include "bleinfo.h"

namespace BleAdvertisementParser
{
    AirpodsTrayApp::Enums::AirPodsModel modelNameForId(quint16 modelId);
    QString colorNameForId(quint8 colorId);
    QString connectionStateName(BleInfo::ConnectionState state);

    bool parseAppleManufacturerData(const QByteArray &data,
                                    const QString &name,
                                    const QString &address,
                                    BleInfo *deviceInfo);
}

#endif // BLEADVERTISEMENTPARSER_H
