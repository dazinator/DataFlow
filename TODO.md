
## Recent Questions / Decisions

- [x] 1. Event stream based idempotency - is it worth it?
    - No, the idempotency key needs to be able to persist in same transaction as the users job logic. The event stream is saved in this transaction because we need event stream information even for failed jobs.
   


   ## TODO
- [x] Tests for idempotency 
- [ ] UI component should 
  - [ ] build its steps list from the stream
  - [ ] have a way to display DynamicComponent for different step types. Maybe we can make use of event metadata or other things.

## Recent prompts

```
As you can see I am trying to develop blazor wasm components that can display the execution of a pipeline by having hierarchy of components that can process the event stream events for a given execution id.

I want a dynamic, self-organizing visualization system where components render themselves based on the event stream and naturally build up the hierarchy.

Each event in the event stream has the ExecutionId, but it also has the MiddlewareType which is based on the "Step Type". 

At the moment on the Index page of my sample blazor wasp app, I have a Controller component where I can click a button and execute a pipeline configured in Program.cs

```

problems:-

Lets call the main component that renders the pipeline "main"
Lets call the components for rendering middleware "middleware"
Lets allow "middleware" components to be nested.

When main component subscribes it wants an event stream for the top level execution id.
As soon as an event is encountered for a middleware it will want to render a component for that middleware that can render the event stream for that middleware. 
However at this point the first event for the middlewarehas already been handled. When the middleware component is then rendered - if it only starts observing future events it may miss the step started​event that caused it to be added! 

Some thoughts on solutions:-

1. More sophisiticated event stream handling
  - When a new subcomponent is added for the middleware in the event stream, we can pass the "started" event that caused it to be added, as a paramater to the component. This component then won't miss the initial event. Perhaps we also pass the Observable event stream - and this component can "take over" so all events from that point onwards are being processed by it. This means it can update its display however it wants
    - In other words we delegate control fo the stream to the correct component in the hierarchy based on the order in which we encounter "middleware type" in the event stream events - adding the components for each middleware type dynamically as we go - and passign the "current" event each time so it doesn't miss it.
  - The "main" component is observing for a particular execution id, but we know that when processing branches, we expect event stream events for the branch execution id's with parent execution id's relating to the initial pipeline execution. Let's assume that a "BranchPerItem" component has taken control of the event stream as suggested above, then it would be expecting to observe these events with the new ExecutionId's relating to the branches. However the origirnal stream is currently constrained to only the main execution ID and not branchhes.
    - therefore perhaps this component needs a way to subscribe to new observables when it is initialised so it can pick up child branches of the current Execution Id event it is initialised with. For example it would be expecting to encounter a new branch per item - so the branching is onlyt level from the current execution id for the main branch.
      - Perhaps it could render each one by rednering a "main" component again per branch execution id - so we get some composability? 

So in this model, the PipelineVisualisation component would be responsible for rendering the top level pipeline execution id, as well as any branches (again by execution id). When it encounters a middleware type in the event stream, it would add a new component for that middleware type as well as pass the current event as a paramater.
This component would then "take over" event stream processing, and in the case of Branch Per Item middleware this means subscribing to new observables that can detect branch execution ids. We'd need to be able to detect when control over the event stream processing for the current component should end - e.g based on a Completed or Failed event so that we can delegate control back to the parent component.
In other words, in this structure, there would be
- 1 event stream
- control of the event stream would be delegated to sub components as they are added to the hierarchy - and the current event passed as an arg to avoid buffering.
- control of the event stream would be returned to the parent component when the sub component is done processing events for the current execution id.
- Different subcomponents may need to effectively create branches of the event stream by creating new observables - to observe branches of the current execution id. They could pass that stream to new subcomponents that they add to the hierarchy instead of the intial stream passed to them.

```

1. It could then pass the buffered event stream to the middleware component when it is added. This would allow the middleware component to render the step started event that caused it to be added. However, this would mean that the "main" component would need to be able to buffer all events for all execution id's - which could be a lot of events.
1. 
1. then when a middleware component is added, it can be passed the buffered events. This would allow it to render the step started event that caused it to be added. However, this would mean that the "main" component would need to be able to buffer all events for all execution id's - which could be a lot of events.
If we knew the structure, in terms of which components / middlewares would be needed and in what order from the outset, we might be able to set up the component hierarchy in advance - but we don't know this. 

Do you have any thoughts about how we might deal with these problems?

```
I think we need to re-think this component:


Copy
and its code behind


Copy
There are some issues:-

These things aren't really working


Copy
private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, StepState>> _producerStepStates = new();
private readonly ConcurrentDictionary<string, ConcurrentDictionary<int, StepState>> _consumerStepStates = new();
private readonly ConcurrentDictionary<int, (int currentItem, bool isWaiting)> _consumerState = new();
When a pipeline is executing and we are receiving events, I want to
* Allow each middleware type in the pipeline (like Step or Producer Consumer or Branch etc, to display the step using its own UI component. We could perhaps use Blazor DynamicComponent. We might need to extend PipelineExecutionEvent to also have the middleware type name.
* Due to the above, the events in the event stream that the UI component is responsible for - need to be handled by this UI component - so for example, if a Step A was a simple step middleware, and Step B was a producer consumer middleware, then the respective component should be added using Blazor DynamicComponent. In addition that component should be receiving that portion of the event stream so it can update itself. This also means that if a component creates branches of the pipeline, the events need to be routed from the parent component through each level to the child component that is displaying. So for example if Step B was a Branch middleware, then inside the branch, there was a Producer Consumer step, and in the Consumer branch there was a normal Step, we'd want the top level PipelineVisualisation.razor to use the DynamicComponent for step B and so select the "Branch" UI component. The event stream for "Branch" middleware events and the current execution id would be picked up by this Branch component and used to render itself. It would subscribe to all events for the branches execution ID, and so it would now receive events for each middleware executing in the branch execution - for each it would render the correct component using DynamicComponent - so for the Producer Consumer step it would render that component. The Producer Consumer step creates additional branch hierarchies - so it would want to subscribe to all relevent events for those execution id's so it could update its display. It chooses for example to render its Producer and Consumer branch as containers, and then for each parallel producer branch it renders boxes inside the Producer container and vice versa. It also renders a Stats component to show the buffer stats, items produced and consumed etc.

So what I am after is an approach that allows a hierarchical component model, allowing different types of steps / middlewares to be responsible for subscribing and rendering their portions of the event stream - at the top level everything will initially be constrained by the execution ID.

Finally, I am not sure whether it makes sense to have to manually populate this


Copy
    [Parameter] public List<PipelineStep> MainSteps { get; set; } = new();
Ideally i want the Steps auto discovered as they are encountered on the event stream. This means we could use this component to visualise any pipeline, without having to tell it all the Steps in advance which mist be matched up to what is executed. Does this make sense to you?

```

 
 ```
 Yes, this makes perfect sense! You want a more dynamic, self-organizing visualization system where components render themselves based on the event stream and naturally build up the hierarchy. Let me outline an approach:

 ```


