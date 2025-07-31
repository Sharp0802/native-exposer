#include "lib.h"

#include <exception>
#include <filesystem>
#include <iostream>
#include <sstream>
#include <string>
#include <unordered_map>
#include <utility>

#include <coreclr_delegates.h>
#include <hostfxr.h>

#if _WIN32
#include <windows.h>
#elif __APPLE__
#include <mach-o/dyld.h>
#else
#include <unistd.h>
#include <linux/limits.h>
#endif

#ifndef HOSTFXR_IMPORT
#define HOSTFXR_IMPORT(name) std::remove_pointer_t<hostfxr_##name##_fn> name __asm__("hostfxr_"#name)
#endif

namespace clr {
  std::string to_string(const StatusCode code) {
    std::unordered_map<StatusCode, std::string> map = {
      {Success, "Operation was successful"},
      {
        Success_HostAlreadyInitialized, "Initialization was successful, but another host context is already initialized"
      },
      {
        Success_DifferentRuntimeProperties,
        "Initialization was successful, but another host context is already initialized and the requested context specified runtime properties which are not the same"
      },
      {InvalidArgFailure, "One or more arguments are invalid"},
      {CoreHostLibLoadFailure, "Failed to load a hosting component"},
      {CoreHostLibMissingFailure, "One of the hosting components is missing"},
      {CoreHostEntryPointFailure, "One of the hosting components is missing a required entry point"},
      {
        CurrentHostFindFailure,
        "Failed to get the path of the current hosting component and determine the .NET installation location"
      },
      {CoreClrResolveFailure, "The `coreclr` library could not be found"},
      {CoreClrBindFailure, "Failed to load the `coreclr` library or finding one of the required entry points"},
      {CoreClrInitFailure, "Call to `coreclr_initialize` failed"},
      {CoreClrExeFailure, "Call to `coreclr_execute_assembly` failed"},
      {ResolverInitFailure, "Initialization of the `hostpolicy` dependency resolver failed"},
      {ResolverResolveFailure, "Resolution of dependencies in `hostpolicy` failed"},
      {LibHostInitFailure, "Initialization of the `hostpolicy` library failed"},
      {LibHostInvalidArgs, "Arguments to `hostpolicy` are invalid"},
      {InvalidConfigFile, "The `.runtimeconfig.json` file is invalid"},
      {AppArgNotRunnable, "[internal usage only]"},
      {AppHostExeNotBoundFailure, "`apphost` failed to determine which application to run"},
      {FrameworkMissingFailure, "Failed to find a compatible framework version"},
      {HostApiFailed, "Host command failed"},
      {HostApiBufferTooSmall, "Buffer provided to a host API is too small to fit the requested value"},
      {AppPathFindFailure, "Application path imprinted in `apphost` doesn't exist"},
      {SdkResolveFailure, "Failed to find the requested SDK"},
      {FrameworkCompatFailure, "Application has multiple references to the same framework which are not compatible"},
      {FrameworkCompatRetry, "[internal usage only]"},
      {BundleExtractionFailure, "Error extracting single-file bundle"},
      {BundleExtractionIOError, "Error reading or writing files during single-file bundle extraction"},
      {
        LibHostDuplicateProperty,
        "The application's `.runtimeconfig.json` contains a runtime property which is produced by the hosting layer"
      },
      {
        HostApiUnsupportedVersion,
        "Feature which requires certain version of the hosting layer was used on a version which doesn't support it"
      },
      {HostInvalidState, "Current state is incompatible with the requested operation"},
      {HostPropertyNotFound, "Property requested by `hostfxr_get_runtime_property_value` doesn't exist"},
      {HostIncompatibleConfig, "Host configuration is incompatible with existing host context"},
      {HostApiUnsupportedScenario, "Hosting API does not support the requested scenario"},
      {HostFeatureDisabled, "Support for a requested feature is disabled"}
    };

    const auto it = map.find(code);
    if (it == map.end()) {
      return "Unknown error; May be HRESULT code";
    }

    return it->second;
  }
}

namespace hostfxr {
  HOSTFXR_IMPORT(initialize_for_runtime_config);
  HOSTFXR_IMPORT(get_runtime_delegate);
  HOSTFXR_IMPORT(close);
}

namespace clr {
  static get_function_pointer_fn get_function_pointer;
  static load_assembly_fn load_assembly;
}

static hostfxr_handle global_hostfxr;

using ustring = std::basic_string<clr::uchar_t>;

static ustring get_executable_path() {
#ifdef _WIN32
  clr::uchar_t buffer[MAX_PATH];
  GetModuleFileNameW(NULL, buffer, MAX_PATH);
  return buffer;
#elif __APPLE__
  clr::uchar_t buffer[8192];
  uint32_t size = sizeof(buffer);
  if (_NSGetExecutablePath(buffer, &size) == 0) {
    return buffer;
  }
  
  return "";
#else // Linux and other POSIX-compliant systems
  clr::uchar_t buffer[PATH_MAX];
  ssize_t len = readlink("/proc/self/exe", buffer, sizeof(buffer) - 1);
  if (len != -1) {
    buffer[len] = '\0';
    return buffer;
  }
  
  return "";
#endif
}

static ustring base_path(const ustring& path)
{
  std::filesystem::path f(path);
  f = f.parent_path();
#if _WIN32
  return f.wstring();
#else
  return f.string();
#endif
}

void clr::assert_status_code(StatusCode code)
{
  if (code == 0)
    return;

  std::stringstream s;
  s << "CLR error 0x" << std::hex << code << std::dec << ": " << clr::to_string(code) << std::endl;
  throw std::runtime_error(s.str());
}

clr::StatusCode clr::init(const uchar_t *dotnetRoot, const uchar_t *runtimeConfigPath) {
  const ustring host_path = base_path(get_executable_path());
  const ::hostfxr_initialize_parameters params{
    .size = sizeof params,
    .host_path = host_path.c_str(),
    .dotnet_root = dotnetRoot
  };
  
  int r = hostfxr::initialize_for_runtime_config(runtimeConfigPath, &params, &global_hostfxr);
  if (r) {
    return static_cast<StatusCode>(r);
  }

  r = hostfxr::get_runtime_delegate(global_hostfxr, hdt_get_function_pointer, reinterpret_cast<void**>(&get_function_pointer));
  if (r) {
    return static_cast<StatusCode>(r);
  }

  r = hostfxr::get_runtime_delegate(global_hostfxr, hdt_load_assembly, reinterpret_cast<void**>(&load_assembly));
  if (r) {
    return static_cast<StatusCode>(r);
  }

  return Success;
}

clr::StatusCode clr::load(const uchar_t *assemblyPath) {
  int r = load_assembly(assemblyPath, nullptr, nullptr);
  return static_cast<StatusCode>(r);
}

clr::StatusCode clr::close()
{
  return static_cast<StatusCode>(hostfxr::close(global_hostfxr));
}


#ifndef MANAGED_CALL
#define MANAGED_CALL __attribute__((__cdecl__))
#endif
