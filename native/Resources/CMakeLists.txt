##
##                @LIBRARY@ : Name of project
## @DOTNET_RUNTIME_VERSION@ : Version of .NET runtime
##

cmake_minimum_required(VERSION 3.21)
project(@LIBRARY@ LANGUAGES CXX)

include(FetchContent)

# Options

set(DOTNET_RUNTIME_ID "" CACHE STRING "RID of target architecture (e.g. linux-x64, win-x64)")
if ("${DOTNET_RUNTIME_ID}" STREQUAL "")
    message(FATAL_ERROR "Required variable DOTNET_RUNTIME_ID is not set.")
endif ()

# Loads nethost libraries

FetchContent_Declare(
        nethost_dependency
        DOWNLOAD_EXTRACT_TIMESTAMP ON
        URL "https://www.nuget.org/api/v2/package/runtime.${DOTNET_RUNTIME_ID}.Microsoft.NETCore.DotNetAppHost/@DOTNET_RUNTIME_VERSION@"
)
FetchContent_MakeAvailable(nethost_dependency)

FetchContent_GetProperties(nethost_dependency SOURCE_DIR nethost_source_dir)
set(NETHOST_NATIVE_DIR "${nethost_source_dir}/runtimes/${DOTNET_RUNTIME_ID}/native")

if(DOTNET_RUNTIME_ID MATCHES "win$")
    set(HOSTFXR_LINK_LIB "${NETHOST_NATIVE_DIR}/hostfxr.lib")
else() # Linux and other Unix-like systems
    set(HOSTFXR_LINK_LIB "${NETHOST_NATIVE_DIR}/libhostfxr.a")
endif()

add_library(nethost INTERFACE)
target_include_directories(nethost INTERFACE "${NETHOST_NATIVE_DIR}")
target_link_libraries(nethost INTERFACE "${HOSTFXR_LINK_LIB}")

# Create bridge library

add_library(@LIBRARY@ STATIC lib.cxx lib.h)
target_compile_features(@LIBRARY@ PUBLIC cxx_std_17)
target_include_directories(@LIBRARY@ PUBLIC "${CMAKE_CURRENT_SOURCE_DIR}")
target_link_libraries(@LIBRARY@ PUBLIC nethost)
