cmake_minimum_required(VERSION 3.16)

project(linux VERSION 0.1 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD_REQUIRED ON)

if(WIN32)
    find_package(Qt6 REQUIRED COMPONENTS Core Widgets Qml Quick QuickControls2)

    qt_standard_project_setup()

    qt_add_executable(librepods-ble-parser-smoke
        tests/ble_parser_smoke.cpp
        enums.h
        ble/bleadvertisementparser.cpp
        ble/bleadvertisementparser.h
        ble/iblescanner.h
        ble/bleinfo.h
    )

    target_link_libraries(librepods-ble-parser-smoke
        PRIVATE Qt6::Core
    )

    qt_add_executable(librepods-windows-ble-scanner-smoke
        tests/windows_ble_scanner_smoke.cpp
        enums.h
        ble/bleadvertisementparser.cpp
        ble/bleadvertisementparser.h
        ble/iblescanner.h
        ble/bleinfo.h
        platform/windows/windowsblescanner.cpp
        platform/windows/windowsblescanner.h
    )

    target_compile_features(librepods-windows-ble-scanner-smoke PRIVATE cxx_std_20)

    target_include_directories(librepods-windows-ble-scanner-smoke PRIVATE
        "C:/Program Files (x86)/Windows Kits/10/Include/10.0.26100.0/cppwinrt"
    )

    target_link_libraries(librepods-windows-ble-scanner-smoke
        PRIVATE Qt6::Core windowsapp
    )

    qt_add_executable(librepods-windows-tray-mvp
        platform/windows/windowstraymain.cpp
        enums.h
        ble/bleadvertisementparser.cpp
        ble/bleadvertisementparser.h
        ble/iblescanner.h
        ble/bleinfo.h
        platform/windows/windowsairpodsstate.h
        platform/windows/windowsblescanner.cpp
        platform/windows/windowsblescanner.h
    )

    target_compile_features(librepods-windows-tray-mvp PRIVATE cxx_std_20)

    target_include_directories(librepods-windows-tray-mvp PRIVATE
        "C:/Program Files (x86)/Windows Kits/10/Include/10.0.26100.0/cppwinrt"
    )

    qt_add_resources(librepods-windows-tray-mvp "windows_tray_icons"
        PREFIX "/icons"
        FILES
            assets/airpods.png
    )

    qt_add_resources(librepods-windows-tray-mvp "windows_tray_qml"
        PREFIX "/qml"
        BASE platform/windows/qml
        FILES
            platform/windows/qml/Tray.qml
            platform/windows/qml/BatteryIndicator.qml
    )

    target_link_libraries(librepods-windows-tray-mvp
        PRIVATE Qt6::Core Qt6::Widgets Qt6::Qml Qt6::Quick Qt6::QuickControls2 windowsapp
    )

    # Native AACP SDP/socket probe — no Qt, pure Win32 Bluetooth + Winsock
    add_executable(librepods-windows-aacp-probe
        tests/windows_aacp_probe.cpp
    )

    target_compile_features(librepods-windows-aacp-probe PRIVATE cxx_std_17)

    # Bthprops: BluetoothFindFirstDevice/BluetoothFindNextDevice
    # ws2_32:   WSALookupService*, socket/connect/select
    target_link_libraries(librepods-windows-aacp-probe
        PRIVATE Bthprops ws2_32
    )

    # HCI IOCTL probe — DeviceIoControl on Bluetooth radio handle, no Qt
    add_executable(librepods-windows-hci-ioctl-probe
        tests/windows_hci_ioctl_probe.cpp
    )

    target_compile_features(librepods-windows-hci-ioctl-probe PRIVATE cxx_std_17)

    target_include_directories(librepods-windows-hci-ioctl-probe PRIVATE
        "C:/Program Files (x86)/Windows Kits/10/Include/10.0.26100.0/shared"
    )

    # Bthprops: BluetoothFindFirstRadio, BluetoothFindFirstDevice
    # kernel32: DeviceIoControl (already linked by default, but explicit)
    target_link_libraries(librepods-windows-hci-ioctl-probe
        PRIVATE Bthprops
    )

    return()
endif()

find_package(Qt6 REQUIRED COMPONENTS Quick Widgets Bluetooth DBus LinguistTools)
find_package(OpenSSL REQUIRED)
find_package(PkgConfig REQUIRED)
pkg_check_modules(PULSEAUDIO REQUIRED libpulse)

qt_standard_project_setup()

# Translation files
set(TS_FILES
    translations/librepods_tr.ts
)

qt_add_executable(librepods
    main.cpp
    logger.h
    media/mediacontroller.cpp
    media/mediacontroller.h
    media/pulseaudiocontroller.cpp
    media/pulseaudiocontroller.h
    airpods_packets.h
    trayiconmanager.cpp
    trayiconmanager.h
    enums.h
    battery.hpp
    BluetoothMonitor.cpp
    BluetoothMonitor.h
    autostartmanager.hpp
    BasicControlCommand.hpp
    deviceinfo.hpp
    ble/bleutils.cpp
    ble/bleutils.h
    ble/bleadvertisementparser.cpp
    ble/bleadvertisementparser.h
    ble/iblescanner.h
    ble/bleinfo.h
    ble/blemanager.cpp
    ble/blemanager.h
    thirdparty/QR-Code-generator/qrcodegen.cpp
    thirdparty/QR-Code-generator/qrcodegen.hpp
    QRCodeImageProvider.hpp
    eardetection.hpp
    media/playerstatuswatcher.cpp
    media/playerstatuswatcher.h
    systemsleepmonitor.hpp
)

qt_add_qml_module(librepods
    URI linux
    VERSION 1.0
    QML_FILES
        Main.qml
        BatteryIndicator.qml
        SegmentedControl.qml
        PodColumn.qml
        Icon.qml
        KeysQRDialog.qml
)

# Add the resource file
qt_add_resources(librepods "resources"
    PREFIX "/icons"
    FILES
        assets/airpods.png
        assets/pod.png
        assets/pod_case.png
        assets/pod3.png
        assets/pod3_case.png
        assets/pod4_case.png
        assets/podpro.png
        assets/podpro_case.png
        assets/podmax.png
        assets/fonts/SF-Symbols-6.ttf
)

target_link_libraries(librepods
    PRIVATE Qt6::Quick Qt6::Widgets Qt6::Bluetooth Qt6::DBus OpenSSL::SSL OpenSSL::Crypto ${PULSEAUDIO_LIBRARIES}
)

qt_add_executable(librepods-ctl
    librepods-ctl.cpp
)
target_link_libraries(librepods-ctl
    PRIVATE Qt6::Core Qt6::Network
)
install(TARGETS librepods-ctl
    RUNTIME DESTINATION ${CMAKE_INSTALL_BINDIR}
)

target_include_directories(librepods PRIVATE ${PULSEAUDIO_INCLUDE_DIRS})

include(GNUInstallDirs)
install(TARGETS librepods
    BUNDLE DESTINATION .
    LIBRARY DESTINATION ${CMAKE_INSTALL_LIBDIR}
    RUNTIME DESTINATION ${CMAKE_INSTALL_BINDIR}
)
install(FILES assets/me.kavishdevar.librepods.desktop
    DESTINATION "${CMAKE_INSTALL_DATAROOTDIR}/applications")
install(FILES assets/librepods.svg
    DESTINATION "${CMAKE_INSTALL_DATAROOTDIR}/icons/hicolor/scalable/apps")

# Translation support
qt_add_translations(librepods
    TS_FILES ${TS_FILES}
    QM_FILES_OUTPUT_VARIABLE QM_FILES
)

# Install translation files
install(FILES ${QM_FILES}
    DESTINATION "${CMAKE_INSTALL_DATAROOTDIR}/librepods/translations")
