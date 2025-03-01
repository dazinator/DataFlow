## Decision
1. 1. There is no "re-executing" a data flow.
    1. You build it, you execute it once, and you're done.
    2. If it errors, and you want to re-run it, you have to build a new instance.
### Justification
This decision was made to simplify the design of the system. It is easier to reason about the system if we can assume that data flows are executed once and only once - even if they are long running.
This also simplifies the implementation of the system, as we do not need to worry about the complexities of re-executing a data flow.


