Test Scenario Design:
1. Multiple Active Routes ✅

Creates 3 routes: "slow-route-A", "slow-route-B", "slow-route-C"
Each route gets 5 items (15 total items)

2. Lengthy Processing ✅

Each item takes 12 seconds to process
This is much longer than the route expiration time

3. Quick Source Completion ✅

Producer sends all 15 items quickly (100ms between items = ~1.5 seconds total)
Producer finishes while routes are still processing
This triggers the routing block to start disposal while routes are active

4. Short Route Expiration ✅

Routes expire after 5 seconds
But items take 12 seconds to process
This creates the exact timing issue you described

Expected Behavior (The Bug):

Items 1-15 sent quickly (~1.5 seconds)
Routes start processing (12 seconds each)
After 5 seconds: Routes expire and disposal begins
Disposal timeout: Routes don't complete within 1 minute timeout
"Timeout waiting for route execution" logged
Routes disposed forcefully while still processing
Items lost: Some items that started processing never complete

Key Metrics to Watch:

ItemsProduced: Should be 15
ItemsStartedProcessing: Should be 15 (hopefully)
ItemsCompletedProcessing: Should be less than 15 (the bug!)
TimeoutWarningsDetected: Should be > 0 if we capture the logs
