#ifndef WINDOWS_AIRPODS_STATE_H
#define WINDOWS_AIRPODS_STATE_H

#include <QObject>
#include <QString>
#include <QTimer>

#include "../../ble/bleinfo.h"

// Windows-side QML-facing state. Property names mirror linux/battery.hpp and
// linux/deviceinfo.hpp so that a future merge with the Linux model is mostly
// renaming. Currently fed only by BLE advertisement data; AACP socket data
// will plug in later.
class WindowsAirPodsState : public QObject
{
    Q_OBJECT
    Q_PROPERTY(QString deviceName READ deviceName NOTIFY changed)
    Q_PROPERTY(bool connected READ connected NOTIFY changed)
    Q_PROPERTY(int leftPodLevel READ leftPodLevel NOTIFY changed)
    Q_PROPERTY(bool leftPodCharging READ leftPodCharging NOTIFY changed)
    Q_PROPERTY(bool leftPodAvailable READ leftPodAvailable NOTIFY changed)
    Q_PROPERTY(int rightPodLevel READ rightPodLevel NOTIFY changed)
    Q_PROPERTY(bool rightPodCharging READ rightPodCharging NOTIFY changed)
    Q_PROPERTY(bool rightPodAvailable READ rightPodAvailable NOTIFY changed)
    Q_PROPERTY(int caseLevel READ caseLevel NOTIFY changed)
    Q_PROPERTY(bool caseCharging READ caseCharging NOTIFY changed)
    Q_PROPERTY(bool caseAvailable READ caseAvailable NOTIFY changed)

public:
    explicit WindowsAirPodsState(QObject *parent = nullptr) : QObject(parent)
    {
        m_staleTimer.setInterval(30 * 1000);
        m_staleTimer.setSingleShot(true);
        connect(&m_staleTimer, &QTimer::timeout, this, [this]() {
            if (m_connected) {
                m_connected = false;
                emit changed();
            }
        });
    }

    QString deviceName() const { return m_deviceName; }
    bool connected() const { return m_connected; }
    int leftPodLevel() const { return m_leftPodLevel < 0 ? 0 : m_leftPodLevel; }
    bool leftPodCharging() const { return m_leftPodCharging; }
    bool leftPodAvailable() const { return m_leftPodLevel >= 0; }
    int rightPodLevel() const { return m_rightPodLevel < 0 ? 0 : m_rightPodLevel; }
    bool rightPodCharging() const { return m_rightPodCharging; }
    bool rightPodAvailable() const { return m_rightPodLevel >= 0; }
    int caseLevel() const { return m_caseLevel < 0 ? 0 : m_caseLevel; }
    bool caseCharging() const { return m_caseCharging; }
    bool caseAvailable() const { return m_caseLevel >= 0; }

public slots:
    void updateFromBleInfo(const BleInfo &info)
    {
        const QString name = info.name.isEmpty() ? QStringLiteral("AirPods") : info.name;
        bool dirty = false;
        if (m_deviceName != name) { m_deviceName = name; dirty = true; }
        if (m_leftPodLevel != info.leftPodBattery) { m_leftPodLevel = info.leftPodBattery; dirty = true; }
        if (m_rightPodLevel != info.rightPodBattery) { m_rightPodLevel = info.rightPodBattery; dirty = true; }
        if (m_caseLevel != info.caseBattery) { m_caseLevel = info.caseBattery; dirty = true; }
        if (m_leftPodCharging != info.leftCharging) { m_leftPodCharging = info.leftCharging; dirty = true; }
        if (m_rightPodCharging != info.rightCharging) { m_rightPodCharging = info.rightCharging; dirty = true; }
        if (m_caseCharging != info.caseCharging) { m_caseCharging = info.caseCharging; dirty = true; }
        if (!m_connected) { m_connected = true; dirty = true; }
        m_staleTimer.start();
        if (dirty) emit changed();
    }

signals:
    void changed();

private:
    QString m_deviceName;
    bool m_connected = false;
    int m_leftPodLevel = -1;
    int m_rightPodLevel = -1;
    int m_caseLevel = -1;
    bool m_leftPodCharging = false;
    bool m_rightPodCharging = false;
    bool m_caseCharging = false;
    QTimer m_staleTimer;
};

#endif // WINDOWS_AIRPODS_STATE_H
