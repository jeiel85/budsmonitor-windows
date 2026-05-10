import QtQuick 2.15
import QtQuick.Controls 2.15
import QtQuick.Layouts 1.15

Rectangle {
    id: root

    property int batteryLevel: 50
    property bool isCharging: false
    property string indicator: ""

    readonly property bool darkMode: palette.window.hslLightness < palette.windowText.hslLightness

    readonly property color batteryLowColor: "#FF453A"
    readonly property color batteryMediumColor: "#FFD60A"
    readonly property color batteryHighColor: "#30D158"
    readonly property color chargingColor: "#30D158"
    readonly property color backgroundColor: palette.buttonText
    readonly property color indicatorTextColor: palette.window
    readonly property color textColor: palette.text
    readonly property color borderColor: darkMode ? Qt.rgba(1, 1, 1, 0.3) : Qt.rgba(0, 0, 0, 0.3)

    width: 85
    height: 40
    color: "transparent"

    SystemPalette { id: palette }

    readonly property color levelColor: {
        if (isCharging) return chargingColor;
        if (batteryLevel <= 20) return batteryLowColor;
        if (batteryLevel <= 50) return batteryMediumColor;
        return batteryHighColor;
    }

    ColumnLayout {
        anchors.fill: parent
        spacing: 7

        Item {
            id: batteryIcon
            Layout.preferredWidth: 32
            Layout.preferredHeight: 16
            Layout.alignment: Qt.AlignHCenter

            Rectangle {
                id: batteryBody
                width: parent.width - 2
                height: parent.height
                radius: 3
                color: "transparent"
                border.width: 1.5
                border.color: root.borderColor

                Rectangle {
                    id: batteryFill
                    width: Math.max(2, (batteryBody.width - 4) * (root.batteryLevel / 100))
                    height: batteryBody.height - 4
                    anchors.left: parent.left
                    anchors.leftMargin: 2
                    anchors.verticalCenter: parent.verticalCenter
                    radius: 1.5
                    color: root.levelColor

                    Behavior on width {
                        NumberAnimation { duration: 300; easing.type: Easing.OutCubic }
                    }
                }
            }

            Rectangle {
                width: 2
                height: 8
                radius: 1
                color: root.borderColor
                anchors.left: batteryBody.right
                anchors.verticalCenter: batteryBody.verticalCenter
            }
        }

        RowLayout {
            Layout.alignment: Qt.AlignHCenter
            spacing: 4

            Rectangle {
                visible: root.indicator !== ""
                Layout.preferredWidth: 16
                Layout.preferredHeight: 16
                radius: width / 2
                color: root.backgroundColor

                Text {
                    anchors.centerIn: parent
                    text: root.indicator
                    color: root.indicatorTextColor
                    font.pixelSize: 10
                }
            }

            Text {
                text: root.batteryLevel + "%"
                color: root.textColor
                font.pixelSize: 12
            }
        }
    }
}
