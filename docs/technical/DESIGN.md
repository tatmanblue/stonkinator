# Project Definition

## Stonks

Stonks application is an application that integrates with AI via APIs to provide technically analysis
of stocks (and other financial instruments) to users. The application will use AI to analyze stock data, identify trends,
and provide insights to users to help them make informed investment decisions.

The primary user of Stonks is speculative day traders and used for educational purposes only.

The application does not need to contain the logic to make the technical analysis itself, but rather it will integrate 
with AI APIs that provide this functionality. The application will be responsible for providing a user interface for users to interact with the AI and view the results of the technical analysis.

For the initial version, both the server and client will run locally on the user's machine and can be assumed 
to single-user.  In the future, we may consider building a cloud version of the application where the server 
runs in the cloud and users can access it from anywhere.  But for now, we will focus on building a local 
version of the application.

## Additional files
- [Assessment 1](STONK_ASSESS_1.md)
- [Assessment 2](STONK_ASSESS_2.md)
- [ChatGPT Assessment 1](STONK_ASSESS_1_CHAT.md)
- [ChatGPT Assessment 2](STONK_ASSESS_2_CHAT.md)
- [Gemini Assessment 1](STONK_ASSESS_1_GEMINI.md)
- [Gemini Assessment 2](STONK_ASSESS_2_GEMINI.md)
- [GROK Assessment 1](STONK_ASSESS_1_GROK.md)
- [GROK Assessment 2](STONK_ASSESS_2_GROK.md)


# High level technical design
Using C# 10

Source code is maintained in the [src](../../src) directory.

The solution file will be maintained in the project root directory.

## Stonks Server
The server will be the backbone of the application logic and will be responsible for handling client
requests, processing data, and communicating with the AI APIs. The server will be built using
ASP.NET Core.  The server will expose APIs that the client applications can consume
to retrieve technical analysis data.

### Server requirements
1. For the first version, server will communicate to clients primarily with gRPC for real time communication, but will also expose RESTful APIs where it make sense.  Since the server will be responsible for making and receiving AI requests, using gRPC will allow the server
to push data to clients in real time as it receives it from the AI APIs.
2. Will communicate with AI agents using both visual analysis (Vision models) and quantitative data analysis (LLM reasoning). 
3. No user (client) authentication.  Server and client will run locally.  Server can "trust" the client.
4. No handling of rate limits.  The server will simply pass through requests to the AI APIs and return the results to the client.  If rate limits are hit, the server will return an error to the client and it will be up to the client to handle it.
5. DotNetEnv will be used for configuration


## Stonks Desktop client
The desktop client will be an application that will run on Windows, Linux and MacOS. The desktop client will provide a user-friendly interface for users to interact with the Stonks server and view the technical analysis results. The desktop client will consume the RESTful APIs exposed by the Stonks server to retrieve data and display it to users.

For the most part, the client is a view into the data and insights provided by the server.  The client may have some additional logic to provide a better user experience, but the majority of the application logic will reside in the server.

The client will be written using Avalonia, which is a cross-platform UI framework for .NET.  Avalonia will allow us to build a single codebase for the desktop client that can run on Windows, Linux and MacOS without needing to maintain separate codebases for each platform.  

### Future client applications
In the future, we may consider building additional client applications such as a web client or a mobile.  As such, it might be good to go ahead and put shared logic into separate library now.



## Additional requirements
1. Neither the server nor the client will handle live/automated trades.
2. Dsiclaimers will need to visible but not intrusive.  The application is for educational purposes only and should not be used for live trading.
3. Installation from github source code should be as simple as possible.  We can use dotnet tools to make installation easier.  We can also consider using a package manager like chocolatey or homebrew to make installation easier.
4. Latency is acceptable for now.
5. shared behaviors or logic (server and client) that we can put into a shared library now to make it easier to build future client applications
6. users provide their own API keys for AI APIs and data sources.  The server will need to handle storing and using these API keys when making requests to the AI APIs and data sources.
7. use abstractions where it makes sense

# Example workflows/use cases

## User wants to analyze a stock
1. User opens the Stonks desktop client.
2. User enters a stock ticker symbol (e.g. AAPL) and selects a date range for analysis.
3. The desktop client sends a request to the Stonks server to analyze the specified stock and date range.
4. The Stonks server retrieves historical stock data for the specified ticker and date range from a market data API (e.g. Polygon, Finnhub).
5. The Stonks server sends the retrieved stock data to the AI APIs for analysis.
6. The AI APIs process the stock data and return technical analysis insights (e.g. identified patterns, trends) to the Stonks server.
7. The Stonks server processes the AI insights and sends the results back to the desktop client.
8. The desktop client displays the technical analysis results to the user in an intuitive format (e.g. charts, summaries).


