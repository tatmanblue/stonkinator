# Technical Assessment 2: Project Stonks

## 1. Stonks Server Analysis

### Q1: RESTful vs. gRPC for Client-Server Communication
* **RESTful APIs (Web API):**
    * **Pros:** High compatibility with web and mobile clients; easy to debug via standard browser tools; stateless and simple to scale.
    * **Cons:** Higher overhead due to JSON serialization; request-response model makes "push" updates (like live ticker prices) less efficient.
* **gRPC:**
    * **Pros:** Extremely high performance with Protobuf (binary) serialization; native support for bi-directional streaming (perfect for live market data); contract-first development ensures client/server sync.
    * **Cons:** More complex setup; requires HTTP/2; less "human-readable" debugging compared to JSON.
* **Recommendation:** Use **gRPC** for the Desktop-to-Server data stream to ensure high performance for traders, but maintain a small **REST** surface for administrative tasks or future web-client authentication.

### Q2: Authentication Handling
* **External (AI/Data):** Use **Environment Variables** or a **Secret Manager** (like Azure Key Vault or AWS Secrets Manager) to store AI API keys. The server should act as a proxy so the Client never sees these keys.
* **Internal (Client-to-Server):** For a desktop-first approach, **OAuth 2.0 with PKCE** or simple **JWT (JSON Web Tokens)** is recommended to manage user sessions securely without storing passwords locally.

### Q3: Data Storage Requirements
* **Requirements:** Need to store user preferences, watchlists, cached AI analysis results (to save on API costs), and historical ticker metadata.
* **Options:** * **PostgreSQL:** Excellent for relational data and robust enough for financial metadata.
    * **SQLite:** Useful if you want the "Server" to be a lightweight local process for a single user.
    * **Redis:** Ideal for caching high-frequency market data or temporary AI responses.

### Q4: Future Proofing
* **Abstraction Layers:** Use the **Repository Pattern** and **Interface-based AI Clients**. This allows you to swap OpenAI for Anthropic (or a local model) simply by changing the implementation of an `IAiAnalyzer` interface.
* **Database:** Use **FluentMigrator** or **EF Core Migrations** to ensure schema changes are version-controlled as the project grows.

### Q5: Visual vs. Quantitative Analysis
* **Quantitative:** Use for "Logic-based" insights (e.g., "RSI is 75, indicating overbought"). This is faster and mathematically precise.
* **Visual:** Use for "Shape-based" insights (e.g., "This looks like a Cup and Handle"). This is great for educational purposes where the user needs to *see* why the AI made a call.
* **Strategy:** Provide Quantitative analysis by default; trigger Visual analysis (VLM) when the user clicks an "Analyze Chart Shape" button.

---

## 2. Stonks Desktop Client Analysis

### Q1: Framework Selection (MAUI vs. Avalonia)
* **Avalonia UI:** Highly recommended for this project. It offers a more consistent cross-platform styling experience (Windows, Linux, macOS) and handles high-performance rendering for complex charts better than MAUI in many desktop-specific scenarios.
* **MAUI:** Better if you prioritize mobile (iOS/Android) in the near future, but it can feel "mobile-first" on a desktop.

### Q2: Specific Features
* **Multi-Pane Charting:** Ability to view different timeframes (1m, 5m, 1h) simultaneously.
* **AI Insights Panel:** A sidebar that updates with natural language explanations of the current chart.
* **Educational Overlay:** A toggle that draws the identified patterns (e.g., the "Neckline" of a Head and Shoulders) directly on the chart.

### Q3: Shared Libraries
* **Domain Models:** Ticker, Candle, AnalysisResult, and UserProfile classes.
* **Validation Logic:** Rules for what constitutes a valid ticker symbol or timeframe.
* **API Client:** A shared library containing the gRPC/REST client logic so future Web/Mobile apps don't have to rewrite the communication stack.

---

## 3. Deployment & Distribution
* **Dotnet Tools:** For a developer-centric audience, distributing the server as a `dotnet tool` is a brilliant move for simplicity.
* **Packaging:** Use **Squirrel.Windows** or **AppImage** for Linux to provide a "one-click" experience for the desktop client, even if the user isn't a developer.
