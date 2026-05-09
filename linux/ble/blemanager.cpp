#include "blemanager.h"
#include "bleadvertisementparser.h"
#include <QDebug>
#include <QTimer>
#include "logger.h"

BleManager::BleManager(QObject *parent) : IBleScanner(parent)
{
    discoveryAgent = new QBluetoothDeviceDiscoveryAgent(this);
    discoveryAgent->setLowEnergyDiscoveryTimeout(0); // Continuous scanning

    connect(discoveryAgent, &QBluetoothDeviceDiscoveryAgent::deviceDiscovered,
            this, &BleManager::onDeviceDiscovered);
    connect(discoveryAgent, &QBluetoothDeviceDiscoveryAgent::finished,
            this, &BleManager::onScanFinished);
    connect(discoveryAgent, &QBluetoothDeviceDiscoveryAgent::errorOccurred,
            this, &BleManager::onErrorOccurred);
}

BleManager::~BleManager()
{
    delete discoveryAgent;
}

void BleManager::startScan()
{
    LOG_DEBUG("Starting BLE scan...");
    discoveryAgent->start(QBluetoothDeviceDiscoveryAgent::LowEnergyMethod);
}

void BleManager::stopScan()
{
    LOG_DEBUG("Stopping BLE scan...");
    discoveryAgent->stop();
}

bool BleManager::isScanning() const
{
    return discoveryAgent->isActive();
}

void BleManager::onDeviceDiscovered(const QBluetoothDeviceInfo &info)
{
    if (!info.manufacturerData().contains(0x004C))
    {
        return;
    }

    BleInfo deviceInfo;
    if (BleAdvertisementParser::parseAppleManufacturerData(
            info.manufacturerData().value(0x004C),
            info.name(),
            info.address().toString(),
            &deviceInfo))
    {
        emit deviceFound(deviceInfo);
    }
}

void BleManager::onScanFinished()
{
    if (discoveryAgent->isActive())
    {
        discoveryAgent->start(QBluetoothDeviceDiscoveryAgent::LowEnergyMethod);
    }
}

void BleManager::onErrorOccurred(QBluetoothDeviceDiscoveryAgent::Error error)
{
    LOG_ERROR("BLE scan error occurred:" << error);
    emit errorOccurred(QString::number(static_cast<int>(error)));
    stopScan();
}
