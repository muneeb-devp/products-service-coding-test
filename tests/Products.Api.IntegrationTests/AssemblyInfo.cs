using Xunit;

// Integration tests run one class at a time.
//
// WebApplicationFactory boots the real entry point, and the hosting
// infrastructure that captures the built host does so through a process-wide
// DiagnosticListener. Two factories building hosts at the same moment interfere
// with each other's capture, and the loser fails with "the entry point exited
// without ever building an IHost" — an error that points nowhere near the cause.
//
// Scoped to this assembly on purpose: the unit test projects have no shared
// process state and keep running in parallel.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
