# Project Definition

## Stonks

Stonks application is an application that integrates with AI via APIs to provide technically analysis
of stocks (and other financial instruments) to users. The application will use AI to analyze stock data, identify trends,
and provide insights to users to help them make informed investment decisions.

The primary user of Stonks is speculative day traders and used for educational purposes only.

The application does not need to contain the logic to make the technical analysis itself, but rather it will integrate with AI APIs that provide this functionality. The application will be responsible for providing a user interface for users to interact with the AI and view the results of the technical analysis.

## Assessment Goal

The goal will be to start building the technical understanding and plan of the project by reviewing the design below, answering the questions presented and adding clarification to additional concerns raised through this assessment.


# High level technical design
Using C# 10

Source code is maintained in the [src](../../src) directory.

The solution file will be maintained in the project root directory.

## Stonks Server
The server will be the backbone of the application logic and will be responsible for handling client 
requests, processing data, and communicating with the AI APIs. The server will be built using 
ASP.NET Core.  The server will expose APIs that the client applications can consume 
to retrieve technical analysis data.

**Questions:**
1. Client to server communication does not have to be restful. GRPC and SignalR provide two way realtime communications.  What the pros and cons of using RESTful APIs vs gRPC for client-server communication in the Stonks application?
2. how will authentication for AI API and data sources be handled in the server? Will we use API keys, OAuth, or another authentication mechanism?
3. What data storage requirements do we have?  And what are the best options?
4. How much future proofing do we want to do in the server design?  Data storage requirements.  Data sources requirements.  AI API integrations etc....
5. When to visual analysis (Vision models) and quantitative data analysis (LLM reasoning)?  It sounds like we could benefit from both depending on needs.

## Stonks Desktop client
The desktop client will be an application that will run on Windows, Linux and MacOS. The desktop client will provide a user-friendly interface for users to interact with the Stonks server and view the technical analysis results. The desktop client will consume the RESTful APIs exposed by the Stonks server to retrieve data and display it to users.

For the most part, the client is a view into the data and insights provided by the server.  The client may have some additional logic to provide a better user experience, but the majority of the application logic will reside in the server.

### Future client applications
In the future, we may consider building additional client applications such as a web client or a mobile.  As such, it might be good to go ahead and put shared logic into separate library now.

**Questions:**
1. Will the desktop client be built using MAUI, Avalonia, or another framework?
2. What are the specific features and functionalities that the desktop client will provide to users?
3. Are there any shared behaviors or logic (server and client) that we can put into a shared library now to make it easier to build future client applications?


## Additional requirements
1. Neither the server nor the client will handle live/automated trades.
2. Dsiclaimers will need to visible but not intrusive.  The application is for educational purposes only and should not be used for live trading.
3. Installation from github source code should be as simple as possible.  We can use dotnet tools to make installation easier.  We can also consider using a package manager like chocolatey or homebrew to make installation easier.
4. Latency is acceptable for now.

# Additional Resources

The attached resources are intended to provide further context and technical insights into the development of the Stonks application. They include:
1. First Assessment: [STONK_ASSESS_1.md](./STONK_ASSESS_1.md)  
2. Response 1: [STONK_ASSESS_1_GEMINI.md](./STONK_ASSESS_1_GEMINI.md)
3. Response 2: [STONK_ASSESS_1_GROK.md](./STONK_ASSESS_1_GROK.md)  
4. Response 3: [STONK_ASSESS_1_CHAT.md](./STONK_ASSESS_1_CHAT.md)
