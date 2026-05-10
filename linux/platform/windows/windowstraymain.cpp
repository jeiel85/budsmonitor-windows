#include "../../ble/iblescanner.h"
#include "windowsairpodsstate.h"
#include "windowsblescanner.h"

#include <QAction>
#include <QApplication>
#include <QGuiApplication>
#include <QMenu>
#include <QQmlApplicationEngine>
#include <QQmlContext>
#include <QQuickWindow>
#include <QScreen>
#include <QSystemTrayIcon>

namespace
{
QString formatBattery(int value)
{
    return value < 0 ? QStringLiteral("n/a") : QStringLiteral("%1%").arg(value);
}

QString tooltipText(const BleInfo &info)
{
    return QStringLiteral("%1 | L %2  R %3  Case %4")
        .arg(info.name.isEmpty() ? QStringLiteral("AirPods") : info.name,
             formatBattery(info.leftPodBattery),
             formatBattery(info.rightPodBattery),
             formatBattery(info.caseBattery));
}

void positionPopover(QQuickWindow *window, const QRect &iconRect)
{
    if (!window) return;
    QScreen *screen = QGuiApplication::screenAt(iconRect.center());
    if (!screen) screen = QGuiApplication::primaryScreen();
    const QRect avail = screen ? screen->availableGeometry() : QRect();

    int x = iconRect.center().x() - window->width() / 2;
    int y = iconRect.top() - window->height() - 8;
    if (y < avail.top()) {
        y = iconRect.bottom() + 8;
    }
    x = qBound(avail.left() + 4, x, avail.right() - window->width() - 4);
    window->setPosition(x, y);
}
}

int main(int argc, char *argv[])
{
    QApplication app(argc, argv);
    QApplication::setQuitOnLastWindowClosed(false);

    WindowsAirPodsState state;

    QQmlApplicationEngine engine;
    engine.rootContext()->setContextProperty(QStringLiteral("airPodsState"), &state);
    engine.load(QUrl(QStringLiteral("qrc:/qml/Tray.qml")));
    if (engine.rootObjects().isEmpty()) {
        return 1;
    }
    auto *popover = qobject_cast<QQuickWindow *>(engine.rootObjects().first());

    QSystemTrayIcon tray(QIcon(QStringLiteral(":/icons/assets/airpods.png")));
    QMenu menu;
    QAction *statusAction = menu.addAction(QStringLiteral("Scanning for AirPods"));
    statusAction->setEnabled(false);
    menu.addSeparator();
    QAction *showAction = menu.addAction(QStringLiteral("Show popover"));
    QAction *restartScanAction = menu.addAction(QStringLiteral("Restart scan"));
    menu.addSeparator();
    QAction *quitAction = menu.addAction(QStringLiteral("Quit"));

    tray.setContextMenu(&menu);
    tray.setToolTip(QStringLiteral("LibrePods: scanning for AirPods"));
    tray.show();

    WindowsBleScanner scanner;
    IBleScanner *scannerInterface = &scanner;

    QObject::connect(scannerInterface, &IBleScanner::deviceFound, &state,
                     &WindowsAirPodsState::updateFromBleInfo);

    QObject::connect(scannerInterface, &IBleScanner::deviceFound, &app, [&](const BleInfo &info) {
        const QString text = tooltipText(info);
        statusAction->setText(text);
        tray.setToolTip(QStringLiteral("LibrePods: %1").arg(text));
    });

    QObject::connect(scannerInterface, &IBleScanner::errorOccurred, &app, [&](const QString &message) {
        statusAction->setText(QStringLiteral("BLE scanner error: %1").arg(message));
        tray.setToolTip(QStringLiteral("LibrePods: BLE scanner error"));
    });

    auto togglePopover = [&]() {
        if (!popover) return;
        if (popover->isVisible()) {
            popover->hide();
        } else {
            positionPopover(popover, tray.geometry());
            popover->show();
            popover->raise();
            popover->requestActivate();
        }
    };

    QObject::connect(&tray, &QSystemTrayIcon::activated, &app,
                     [&](QSystemTrayIcon::ActivationReason reason) {
                         if (reason == QSystemTrayIcon::Trigger) togglePopover();
                     });
    QObject::connect(showAction, &QAction::triggered, &app, togglePopover);

    QObject::connect(restartScanAction, &QAction::triggered, &app, [&]() {
        scannerInterface->stopScan();
        statusAction->setText(QStringLiteral("Scanning for AirPods"));
        tray.setToolTip(QStringLiteral("LibrePods: scanning for AirPods"));
        scannerInterface->startScan();
    });

    QObject::connect(quitAction, &QAction::triggered, &app, [&]() {
        scannerInterface->stopScan();
        if (popover) popover->hide();
        app.quit();
    });

    scannerInterface->startScan();
    return app.exec();
}
