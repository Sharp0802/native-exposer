#include "lib.h"

#include <utility>

#include <coreclr_delegates.h>
#include <hostfxr.h>
#include <iostream>
#include <string>
#include <unordered_map>

#ifndef HOSTFXR_IMPORT
#define HOSTFXR_IMPORT(name) extern "C" std::remove_pointer_t<hostfxr_##name##_fn> name __asm__("hostfxr_"#name)
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
      return "Unknown error";
    }

    return it->second;
  }
}

namespace hostfxr {
  HOSTFXR_IMPORT(initialize_for_runtime_config);
  HOSTFXR_IMPORT(get_runtime_delegate);
}

namespace clr {
  static get_function_pointer_fn get_function_pointer;
  static load_assembly_fn load_assembly;
}

static hostfxr_handle global_hostfxr;

clr::StatusCode clr::init(const uchar_t *runtimeConfigPath) {
  int r = hostfxr::initialize_for_runtime_config(runtimeConfigPath, nullptr, &global_hostfxr);
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
