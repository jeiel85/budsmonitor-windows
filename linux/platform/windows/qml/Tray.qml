import QtQuick 2.15
import QtQuick.Controls 2.15
import QtQuick.Layouts 1.15
import QtQuick.Window 2.15

Window {
    id: popover
    width: 320
    height: 180
    visible: false
    flags: Qt.FramelessWindowHint | Qt.Tool | Qt.WindowStaysOnTopHint
    color: palette.window

    SystemPalette { id: palette }

    Rectangle {
        anchors.fill: parent
        color: palette.window
        border.width: 1
        border.color: Qt.rgba(0, 0, 0, 0.2)

        ColumnLayout {
            anchors.fill: parent
            anchors.margins: 16
            spacing: 12

            Text {
                Layout.fillWidth: true
                text: airPodsState.connected
                      ? airPodsState.deviceName
                      : qsTr("Scanning for AirPods…")
                color: palette.text
                font.pixelSize: 14
                font.bold: true
                elide: Text.ElideRight
            }

            RowLayout {
                Layout.alignment: Qt.AlignHCenter
                spacing: 24
                visible: airPodsState.connected

                BatteryIndicator {
                    visible: airPodsState.leftPodAvailable
                    batteryLevel: airPodsState.leftPodLevel
                    isCharging: airPodsState.leftPodCharging
                    indicator: "L"
                }

                BatteryIndicator {
                    visible: airPodsState.rightPodAvailable
                    batteryLevel: airPodsState.rightPodLevel
                    isCharging: airPodsState.rightPodCharging
                    indicator: "R"
                }

                BatteryIndicator {
                    visible: airPodsState.caseAvailable
                    batteryLevel: airPodsState.caseLevel
                    isCharging: airPodsState.caseCharging
                    indicator: "C"
                }
            }

            Item { Layout.fillHeight: true }
        }
    }
}
