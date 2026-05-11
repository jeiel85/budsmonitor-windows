cmake_minimum_required(VERSION 3.16)

project(linux VERSION 0.1 LANGUAGES CXX)

set(CMAKE_CXX_STANDARD_REQUIRED ON)

if(WIN32)
    find_package(Qt6 REQUIRED COMPONENTS Core Widgets Qml Quick QuickControls2)

    qt_standard_project_setup()

    # Locate the Windows SDK cppwinrt headers dynamically so this builds on any
    # machine with a recent Windows 10/11 SDK (not just the developer's box).
    # Picks the highest-versioned installed SDK that ships cppwinrt.
    set(_winsdk_roots
        "C:/Program Files (x86)/Windows Kits/10/Include"
        "C:/Program Files/Windows Kits/10/Include"
    )
    set(WINSDK_CPPWINRT_DIR "" CACHE PATH "Path to Windows SDK cppwinrt headers")
    if(NOT WINSDK_CPPWINRT_DIR)
        foreach(_root IN LISTS _winsdk_roots)
            if(EXISTS "${_root}")
                file(GLOB _versions RELATIVE "${_root}" "${_root}/10.*")
                list(SORT _versions)
                list(REVERSE _versions)
                foreach(_v IN LISTS _versions)
                    if(EXISTS "${_root}/${_v}/cppwinrt")
                        set(WINSDK_CPPWINRT_DIR "${_root}/${_v}/cppwinrt")
                        break()
                    endif()
                endforeach()
            endif()
            if(WINSDK_CPPWINRT_DIR)
                break()
            endif()
        endforeach()
    endif()
    if(NOT WINSDK_CPPWINRT_DIR)
        message(FATAL_ERROR
            "cppwinrt headers not found. Install a Windows 10/11 SDK that includes "
            "cppwinrt, or pass -DWINSDK_CPPWINRT_DIR=<path>.")
    endif()
    message(STATUS "Using cppwinrt headers from: ${WINSDK_CPPWINRT_DIR}")

    # Same idea for the SDK 'shared' headers used by the HCI/L2CAP probes.
    set(WINSDK_SHARED_DIR "" CACHE PATH "Path to Windows SDK shared headers")
    if(NOT WINSDK_SHARED_DIR)
        get_filename_component(_sdk_ver_dir "${WINSDK_CPPWINRT_DIR}" DIRECTORY)
        if(EXISTS "${_sdk_ver_dir}/shared")
            set(WINSDK_SHARED_DIR "${_sdk_ver_dir}/shared")
        endif()
    endif()
    if(WINSDK_SHARED_DIR)
        message(STATUS "Using SDK shared headers from: ${WINSDK_SHARED_DIR}")
    endif()

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
        ${WINSDK_CPPWINRT_DIR}
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
        ${WINSDK_CPPWINRT_DIR}
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
        ${WINSDK_SHARED_DIR}
    )

    # Bthprops: BluetoothFindFirstRadio, BluetoothFindFirstDevice
    # kernel32: DeviceIoControl (already linked by default, but explicit)
    target_link_libraries(librepods-windows-hci-ioctl-probe
        PRIVATE Bthprops
    )

    # Embed UAC manifest so the exe auto-requests administrator elevation on launch
    target_link_options(librepods-windows-hci-ioctl-probe PRIVATE
        "/MANIFESTUAC:level='requireAdministrator' uiAccess='false'"
    )

    # BR/EDR + L2CAP probe — waits for active Classic BT link, then retries L2CAP
    add_executable(librepods-windows-bredr-l2cap-probe
        tests/windows_bredr_l2cap_probe.cpp
    )

    target_compile_features(librepods-windows-bredr-l2cap-probe PRIVATE cxx_std_17)

    target_include_directories(librepods-windows-bredr-l2cap-probe PRIVATE
        ${WINSDK_SHARED_DIR}
    )

    target_link_libraries(librepods-windows-bredr-l2cap-probe
        PRIVATE Bthprops ws2_32
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
