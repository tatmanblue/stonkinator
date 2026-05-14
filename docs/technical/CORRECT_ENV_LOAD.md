## Plan: Use DotNetEnv in Desktop Client

**Objective:** Replace the custom `LoadClientConfig` method in the desktop client with the standard DotNetEnv package, consistent with the server implementation.

**Current State:**
- Server uses `DotNetEnv.Env.Load()` in `Program.cs`
- Desktop client uses custom `LoadClientConfig()` method that manually parses `client.env` file
- Both read from `.env` files but use different approaches

**Proposed Changes:**

### 1. Add DotNetEnv Package to Desktop Client
- Add `<PackageReference Include="DotNetEnv" Version="3.1.1" />` to `Stonks.Client.Desktop.csproj`
- Match the version used by the server project

### 2. Update App.axaml.cs
- Add `using DotNetEnv;` directive
- Replace `LoadClientConfig();` call with `DotNetEnv.Env.Load();`
- Remove the entire `LoadClientConfig()` method (17 lines of custom parsing code)

**Benefits:**
- Consistent configuration loading across server and client
- Leverage battle-tested DotNetEnv library instead of custom parsing
- Support for all DotNetEnv features (comments, quotes, multiline values, etc.)
- Reduced code complexity and maintenance

**Implementation Notes:**
- DotNetEnv automatically looks for `.env` file in the current directory
- The existing `client.env` file will continue to work without changes
- Environment variables are loaded into `Environment.GetEnvironmentVariable()` as before
- No breaking changes to the rest of the application

**Result:** Desktop client now uses the same configuration approach as the server, with cleaner, more maintainable code.
