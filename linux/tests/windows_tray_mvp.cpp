#include "../ble/iblescanner.h"
#include "../platform/windows/windowsblescanner.h"

#include <QAction>
#include <QApplication>
#include <QMenu>
#include <QSystemTrayIcon>

namespace
{
QString formatBattery(int value)
{
    return value < 0 ? QStringLiteral("n/a") : QStringLiteral("%1%").arg(value);
}

QString statusText(const BleInfo &info)
{
    return QStringLiteral("%1 | L %2  R %3  Case %4")
        .arg(info.name.isEmpty() ? QStringLiteral("AirPods") : info.name,
             formatBattery(info.leftPodBattery),
             formatBattery(info.rightPodBattery),
             formatBattery(info.caseBattery));
}
}

int main(int argc, char *argv[])
{
    QApplication app(argc, argv);
    QApplication::setQuitOnLastWindowClosed(false);

    QSystemTrayIcon tray(QIcon(QStringLiteral(":/icons/assets/airpods.png")));
    QMenu menu;
    QAction *statusAction = menu.addAction(QStringLiteral("Scanning for AirPods"));
    statusAction->setEnabled(false);
    menu.addSeparator();

    QAction *restartScanAction = menu.addAction(QStringLiteral("Restart scan"));
    menu.addSeparator();
    QAction *quitAction = menu.addAction(QStringLiteral("Quit"));

    tray.setContextMenu(&menu);
    tray.setToolTip(QStringLiteral("LibrePods: scanning for AirPods"));
    tray.show();

    WindowsBleScanner scanner;
    IBleScanner *scannerInterface = &scanner;

    QObject::connect(scannerInterface, &IBleScanner::deviceFound, &app, [&](const BleInfo &info) {
        const QString text = statusText(info);
        statusAction->setText(text);
        tray.setToolTip(QStringLiteral("LibrePods: %1").arg(text));
    });

    QObject::connect(scannerInterface, &IBleScanner::errorOccurred, &app, [&](const QString &message) {
        statusAction->setText(QStringLiteral("BLE scanner error: %1").arg(message));
        tray.setToolTip(QStringLiteral("LibrePods: BLE scanner error"));
    });

    QObject::connect(restartScanAction, &QAction::triggered, &app, [&]() {
        scannerInterface->stopScan();
        statusAction->setText(QStringLiteral("Scanning for AirPods"));
        tray.setToolTip(QStringLiteral("LibrePods: scanning for AirPods"));
        scannerInterface->startScan();
    });

    QObject::connect(quitAction, &QAction::triggered, &app, [&]() {
        scannerInterface->stopScan();
        app.quit();
    });

    scannerInterface->startScan();
    return app.exec();
}
