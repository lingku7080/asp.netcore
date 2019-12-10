import { groups, BenchmarkEvent, onBenchmarkEvent } from './lib/minibench/minibench.js';
import { HtmlUI } from './lib/minibench/minibench.ui.js';
import './appStartup.js';
import './renderList.js';
import './jsonHandling.js';

new HtmlUI('E2E Performance', '#display');

window.benchmarksResults = [];
onBenchmarkEvent((status, args) => {
    const benchmarksResults = window.benchmarksResults;
    switch (status) {
        case BenchmarkEvent.runStarted:
          benchmarksResults.length = 0;
          break;
        case BenchmarkEvent.benchmarkCompleted:
        case BenchmarkEvent.benchmarkError:
          benchmarksResults.push(args);
          break;
        case BenchmarkEvent.runCompleted:
            console.log("Benchmark completed");
            break;
        default:
          throw new Error(`Unknown status: ${status}`);
      }
});

if (location.href.indexOf('#automated') !== -1) {
  const query = new URLSearchParams(window.location.search);
  const group = query.get('group');

  groups.filter(g => !group || g.name === group).forEach(g => g.runAll());
}
