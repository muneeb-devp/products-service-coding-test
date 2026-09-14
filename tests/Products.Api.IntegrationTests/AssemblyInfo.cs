using Xunit;

// WebApplicationFactory captures the built host via a process-wide
// DiagnosticListener, so concurrent factories clobber each other and fail with
// "the entry point exited without ever building an IHost". Serialise this
// assembly only; the unit test projects still run in parallel.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
