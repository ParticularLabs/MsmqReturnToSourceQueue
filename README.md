Historically, inside the NServiceBus installer, we shipped a tool called ReturnToSourceQueue.exe. ReturnToSourceQueue is a command line tool to facilitate returning failed MSMQ messages from the error queue to the processing queue. 

Since the introduction of the [Platform Installer](http://docs.particular.net/platform/installer/) we no longer ship this tool to developers machines. The reason for this is that ReturnToSourceQueue.exe only supports MSMQ while NServiceBus, and the entire [Particular Service platform](http://docs.particular.net/platform/) supports many different transports. There there are better alternatives to using a command line utility.

The MSMQ ReturnToSourceQueue.exe tool is now deprecated. The code for this tool has been moved to this repository. It will no longer be shipped as part of any NServiceBus tooling.

For people who want the functionality that tool previously provided please take one of the following actions

 1. Return to source queue via either ServiceInsight or ServicePulse. 
 2. Return to source queue using [custom scripting or code](http://docs.particular.net/nservicebus/msmq/operations-scripting). This has the added benefit enabling possible performance and usability optimizations since, as the business owner, you have more context as to how your error queue should be managed. For example using this approach it is trivial for you to choose to batch multiple sends inside the same Transaction.
 3. Manually return to source queue via any of the MSMQ management tools. 
 4. If you still want to use MsmqReturnToSourceQueue.exe feel free to use the code inside this repository to compile a copy.

If, for any reason, the above options do not meet your specific requirements please don't hesitate to contacts about this decision.