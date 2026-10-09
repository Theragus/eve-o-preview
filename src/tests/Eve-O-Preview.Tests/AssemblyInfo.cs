using Xunit;

// The suite spawns private-desktop worker processes and blocks on them, and several classes exchange
// named-pipe messages against one-second budgets in HookService. Running classes in parallel on a
// two-core CI runner starves the thread pool and makes those pipe exchanges time out; the whole
// suite takes well under ten seconds serially.
[assembly: CollectionBehavior(DisableTestParallelization = true)]
