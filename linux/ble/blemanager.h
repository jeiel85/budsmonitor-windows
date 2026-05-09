#ifndef BLEMANAGER_H
#define BLEMANAGER_H

#include <QBluetoothDeviceDiscoveryAgent>
#include "iblescanner.h"

class QTimer;

class BleManager : public IBleScanner
{
    Q_OBJECT
public:
    explicit BleManager(QObject *parent = nullptr);
    ~BleManager() override;

    void startScan() override;
    void stopScan() override;
    bool isScanning() const override;

private slots:
    void onDeviceDiscovered(const QBluetoothDeviceInfo &info);
    void onScanFinished();
    void onErrorOccurred(QBluetoothDeviceDiscoveryAgent::Error error);

private:
    QBluetoothDeviceDiscoveryAgent *discoveryAgent;
};

#endif // BLEMANAGER_H
