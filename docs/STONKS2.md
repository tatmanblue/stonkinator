# Stonkinator V2

This version of the stonkinator is designed to create a pay for service application.

The user will use website or mobile or desktop app to access stonkinator services.  For now, android mobile app will suffice.

# High level architecture

## Cloud services
1. Account management API
2. OHLCV data (cached or in a database if already retrieved otherwise pulls from 3rd party like massive.com)
3. AI 
4. admin api endpoints (approve accounts, close accounts, configure site).  where does the UI live?
5. implement endpoints to use gRPC if it can be used, use that otherwise restful services
6. can run locally for testing, or on cloud services (will start with Azure)


## Android Mobile App
1. TOS
2. Account setup, configures app behaviors
3. All analysis data is stored locally, not on cloud services
4. Same behaviors as desktop app

## iOS mobile app
Defer until android app is functional

## Desktop app
1. user choses to use it as is currently designed or with the service
2. All analysis data is stored locally, not on cloud services
3. Since desktop app has a locally running service, perhaps that stays in place and becomes an intermediary between app and cloud services


## Account management API
1. Create account endpoints.  Consider signup pattern https://github.com/tatmanblue/Cogitatio .  Create account UI in apps not services
2. Determines functionality of app.  eg:  how many requests allowed etc
3. where does the UI live?