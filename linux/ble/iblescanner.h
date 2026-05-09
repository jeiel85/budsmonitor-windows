#ifndef IBLESCANNER_H
#define IBLESCANNER_H

#include <QObject>
#include "bleinfo.h"

class IBleScanner : public QObject
{
    Q_OBJECT
public:
    explicit IBleScanner(QObject *parent = nullptr) : QObject(parent) {}
    ~IBleScanner() override = default;

    virtual void startScan() = 0;
    virtual void stopScan() = 0;
    virtual bool isScanning() const = 0;

signals:
    void deviceFound(const BleInfo &device);
    void errorOccurred(const QString &message);
};

#endif // IBLESCANNER_H
