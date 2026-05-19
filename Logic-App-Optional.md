# Logic App for URL Ingestion (Optional)

> **This is not a Foundry feature.** It is an optional Azure Logic App that automates URL-to-Blob ingestion. For the Foundry POC, manually uploading documents to Blob Storage is sufficient. Set this up later if you want dynamic URL ingestion.

---

## Why

Azure AI Search has no native URL indexer. This Logic App bridges the gap — user pastes a URL, Logic App fetches the content and saves it to Blob Storage, and the AI Search indexer auto-picks it up for chunking/embedding/indexing.

---

## Create the Logic App

1. Go to [Azure Portal](https://portal.azure.com) → **+ Create a resource** → search **Logic App**
2. Select **Logic App (Consumption)** — pay-per-execution, ideal for POC
   - **Name**: e.g., `la-foundry-rag-ingest`
   - **Region**: same as AI Search
   - **Resource Group**: `rp-foundry-project-rg`
3. Click **Review + Create** → **Create**

---

## Build the Workflow (Visual Designer)

4. Go to the Logic App → **Logic app designer**
5. Select **"When an HTTP request is received"** trigger
   - Click **"Use sample payload to generate schema"** and paste:
     ```json
     { "url": "https://example.com/document" }
     ```
   - This generates a schema that accepts a URL in the request body
6. Click **+ New step** → search **HTTP** → select **HTTP** action
   - **Method**: `GET`
   - **URI**: click in the field → select **url** from Dynamic content (from the trigger)
   - This fetches the web page content from the user-provided URL
7. Click **+ New step** → search **Create blob** → select **Create blob (V2)** (Azure Blob Storage)

   **Create connection (first time only):**
   - First, enable the Logic App's managed identity:
     - Go to Logic App → **Identity** → **System assigned** → toggle **On** → **Save**
   - Go to your storage account (`stfoundryrag`) → **Access control (IAM)** → **Add role assignment**:
     - **Role**: `Storage Blob Data Contributor`
     - **Assign to**: **Managed identity** → select your Logic App (`la-foundry-rag-ingest`)
   - Wait ~1-2 minutes for role propagation
   - Back in the designer, create the connection:
     - **Authentication Type**: `Logic Apps Managed Identity`
     - **Connection Name**: e.g., `stfoundryrag-connection`
     - Click **Create**

   **Fill in the action parameters:**
   - **Storage account name or blob endpoint**: `stfoundryrag`
   - **Folder path**: click the folder icon → navigate to `/rag-documents` (or type `/rag-documents`)
   - **Blob name**: click in the field → switch to **Expression** tab (fx) → paste:
     ```
     concat(guid(), '.html')
     ```
     Then click **OK** — this generates a unique filename for each ingested URL
   - **Blob content**: click in the field → switch to **Dynamic content** tab → select **Body** (from the HTTP action)
   - **Content type**: `text/html`
   - Leave all other parameters at their defaults (Infer content type: No, etc.)
8. **(Optional) Trigger the indexer immediately via HTTP action:**
   - The AI Search indexer created in Step 1.4 runs **automatically on a schedule** (default: every 5 minutes) and will pick up new blobs on its own — so this step is optional
   - If you want immediate indexing: Click **+ New step** → search **HTTP** → select **HTTP** action
     - **Method**: `POST`
     - **URI**: `https://rp-search-foundry-rag.search.windows.net/indexers/rp-foundry-rag-indexer/run?api-version=2024-07-01`
     - **Authentication**: select **Managed Identity** → **System-assigned**
     - **Audience**: `https://search.azure.com`
   - ⚠️ *Configure this step after completing Step 1.4 in Foundry-Plan.md, since the indexer won't exist yet*
   - If you skip this, just wait ~5 minutes after the Logic App runs for the indexer to auto-pick up the new blob
9. Click **Save**

---

## Get the Trigger URL

10. Click on the **"When an HTTP request is received"** trigger → copy the **HTTP POST URL**
    - This is the endpoint users will call to ingest a URL
    - Example usage:
      ```bash
      # Bash / curl
      curl -X POST "<your-logic-app-url>" \
        -H "Content-Type: application/json" \
        -d '{"url": "https://www.sec.gov/Archives/edgar/data/1045810/000104581024000316/nvda-20240128.htm"}'
      ```
      ```powershell
      # PowerShell
      Invoke-RestMethod -Method Post -Uri "<your-logic-app-url>" `
        -ContentType "application/json" `
        -Body '{"url": "https://www.sec.gov/Archives/edgar/data/1045810/000104581024000316/nvda-20240128.htm"}'
      ```

---

## How It Works End-to-End

```
User pastes URL → Logic App trigger
                → HTTP GET (fetches page content)
                → Create Blob (saves to rag-documents container)
                → Run Indexer (AI Search chunks, embeds, indexes)
                → Content searchable in minutes ✅
```

> **Tip:** You can also add a **Response** action at the end to return a success message to the caller.
