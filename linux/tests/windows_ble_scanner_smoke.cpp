#include "../ble/iblescanner.h"
#include "../platform/windows/windowsblescanner.h"

#include <QCoreApplication>
#include <QDebug>
#include <QTextStream>
#include <QTimer>

int main(int argc, char *argv[])
{
    QCoreApplication app(argc, argv);

    WindowsBleScanner scanner;
    IBleScanner *scannerInterface = &scanner;
    int foundCount = 0;

    QObject::connect(scannerInterface, &IBleScanner::deviceFound, &app, [&](const BleInfo &info) {
        foundCount++;
        QTextStream(stdout) << "AirPods BLE packet "
                            << info.address
                            << " model=" << static_cast<int>(info.modelName)
                            << " left=" << info.leftPodBattery
                            << " right=" << info.rightPodBattery
                            << " case=" << info.caseBattery
                            << "\n";
    });

    QObject::connect(scannerInterface, &IBleScanner::errorOccurred, &app, [](const QString &message) {
        qWarning() << "Windows BLE scanner error:" << message;
    });

    QTimer::singleShot(15000, &app, [&]() {
        scannerInterface->stopScan();
        QTextStream(stdout) << "Windows BLE scanner smoke finished. Parsed packets: " << foundCount << "\n";
        app.quit();
    });

    QTextStream(stdout) << "Starting Windows BLE scanner smoke for 15 seconds. Open the AirPods case near this PC.\n";
    scannerInterface->startScan();
    return app.exec();
}
