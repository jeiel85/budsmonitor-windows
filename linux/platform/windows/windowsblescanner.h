#ifndef WINDOWSBLESCANNER_H
#define WINDOWSBLESCANNER_H

#include "../../ble/iblescanner.h"

#include <winrt/Windows.Devices.Bluetooth.Advertisement.h>
#include <winrt/Windows.Foundation.h>

class WindowsBleScanner : public IBleScanner
{
    Q_OBJECT
public:
    explicit WindowsBleScanner(QObject *parent = nullptr);
    ~WindowsBleScanner() override;

    void startScan() override;
    void stopScan() override;
    bool isScanning() const override;

private:
    void onAdvertisementReceived(const winrt::Windows::Devices::Bluetooth::Advertisement::BluetoothLEAdvertisementReceivedEventArgs &args);
    void emitDeviceFoundQueued(const BleInfo &info);
    void emitErrorQueued(const QString &message);

    bool m_scanning = false;
    winrt::Windows::Devices::Bluetooth::Advertisement::BluetoothLEAdvertisementWatcher m_watcher{nullptr};
    winrt::event_token m_receivedToken{};
};

#endif // WINDOWSBLESCANNER_H
