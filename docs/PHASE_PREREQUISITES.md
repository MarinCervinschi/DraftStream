# Phase: Prerequisites Setup

**Goal**: Set up all external services before coding begins.

**Estimated steps**: 4 services to configure

---

## Order of Setup

1. **OpenRouter** (simplest, no dependencies)
2. **Infisical** (needed to store other secrets)
3. **Telegram** (bot + group setup)
4. **Notion** (databases + integration)

---

## Step 1: OpenRouter Setup

### 1.1 Create Account

- [X] Go to [openrouter.ai](https://openrouter.ai)
- [X] Sign up or sign in (Google/GitHub OAuth available)

### 1.2 Create API Key

- [X] Navigate to [openrouter.ai/keys](https://openrouter.ai/keys)
- [X] Click "Create Key"
- [X] Name: `DraftStream`
- [X] Copy and save the key (format: `sk-or-v1-...`)

### 1.3 Verify Free Models Available

- [X] Go to [openrouter.ai/models](https://openrouter.ai/models)
- [X] Filter by "Free" pricing
- [X] Confirm these models are available:
    - `meta-llama/llama-3.1-8b-instruct:free`
    - `google/gemma-2-9b-it:free`

### 1.4 Test API (Optional)

```bash
curl https://openrouter.ai/api/v1/chat/completions \
  -H "Authorization: Bearer sk-or-v1-YOUR_KEY" \
  -H "Content-Type: application/json" \
  -d '{
    "model": "meta-llama/llama-3.1-8b-instruct:free",
    "messages": [{"role": "user", "content": "Say hello"}]
  }'
```

**Output**: Save `OPENROUTER_API_KEY`

---

## Step 2: Infisical Setup

### 2.1 Access Your Infisical Instance

- [X] Go to your self-hosted Infisical dashboard
- [X] Log in

### 2.2 Create Project

- [X] Click "Add Project" or "+" button
- [X] Project name: `DraftStream`
- [X] Description: "AI agent for Telegram → Notion workflow"

### 2.3 Configure Environments

- [X] Ensure `dev` environment exists (usually default)
- [X] Optionally create `prod` environment for later

### 2.4 Add Placeholder Secrets

In the `dev` environment, create these secrets (we'll fill values as we go):

| Secret Key                 | Value               | Status  |
|----------------------------|---------------------|---------|
| `OPENROUTER_API_KEY`       | (paste from Step 1) | Done    |
| `TELEGRAM_BOT_TOKEN`       | `placeholder`       | Pending |
| `TELEGRAM_GROUP_ID`        | `placeholder`       | Pending |
| `TELEGRAM_TOPIC_NOTES`     | `placeholder`       | Pending |
| `TELEGRAM_TOPIC_TASKS`     | `placeholder`       | Pending |
| `TELEGRAM_TOPIC_SNIPPETS`  | `placeholder`       | Pending |
| `NOTION_INTEGRATION_TOKEN` | `placeholder`       | Pending |
| `NOTION_DATABASE_NOTES`    | `placeholder`       | Pending |
| `NOTION_DATABASE_TASKS`    | `placeholder`       | Pending |
| `NOTION_DATABASE_SNIPPETS` | `placeholder`       | Pending |

### 2.5 Create Service Token

- [X] Go to Project Settings → Service Tokens (or Access Control → Service Tokens)
- [X] Click "Create Service Token"
- [X] Name: `draftstream-dev`
- [X] Environment: `dev`
- [X] Permissions: Read
- [X] Expiration: Never (or your preference)
- [X] Copy and save the token

### 2.6 Note Infisical Details

Save these for later:

- [X] Infisical URL: `https://your-infisical.example.com`
- [X] Service Token: `st.xxxxx...`
- [X] Project ID: (visible in URL or project settings)

**Output**: Infisical project ready, service token saved

---

## Step 3: Telegram Setup

### 3.1 Create Bot with BotFather

- [X] Open Telegram app
- [X] Search for `@BotFather`
- [X] Start chat, send `/newbot`
- [X] Enter bot name: `DraftStream` (display name)
- [X] Enter username: `your_draftstream_bot` (must end in `bot`, must be unique)
- [X] Copy the bot token (format: `123456789:ABCdefGHIjklMNOpqrsTUVwxyz`)

### 3.2 Configure Bot Settings (Optional but Recommended)

With BotFather:

- [X] `/setdescription` → "AI assistant for notes, tasks, and snippets"
- [X] `/setabouttext` → "Captures your messages and saves them to Notion"
- [X] `/setuserpic` → Upload a bot avatar (optional)

### 3.3 Create Forum Group

- [X] Open Telegram
- [X] Tap hamburger menu (☰) → "New Group"
- [X] Group name: `DraftStream`
- [X] Add your bot (`@your_draftstream_bot`) as member
- [X] Create the group

### 3.4 Enable Topics (Forum Mode)

- [X] Open the group
- [X] Tap group name at top
- [X] Tap "Edit" (pencil icon)
- [X] Scroll down to find "Topics" toggle
- [X] Enable Topics
- [X] Save

**Note**: If you don't see Topics option, the group might need more members or you need to convert to supergroup first.
Try adding another person temporarily.

### 3.5 Create Required Topics

In the group:

- [X] Tap "Create Topic" (or + button)
- [X] Create topic: `Notes` → choose an icon (📝)
- [X] Create topic: `Tasks` → choose an icon (✅)
- [X] Create topic: `Snippets` → choose an icon (💻)

### 3.6 Make Bot an Admin

- [X] Tap group name → "Administrators"
- [X] Tap "Add Administrator"
- [X] Select your bot
- [X] Enable permissions:
    - [x] Post messages
    - [x] Delete messages (optional)
    - [X] Others as needed
- [X] Save

### 3.7 Get Group ID and Topic IDs

**Method: Using @RawDataBot**

- [X] Add `@RawDataBot` to your group
- [X] Go to "General" topic (or any topic)
- [X] Send any message
- [X] RawDataBot replies with JSON - find `"chat": {"id": -100XXXXXXXXXX}`
- [X] That negative number is your **Group ID**

- [X] Go to **Notes** topic, send a message
- [X] Find `"message_thread_id": X` in response - this is **Notes Topic ID**
- [X] Repeat for **Tasks** topic → get Topic ID
- [X] Repeat for **Snippets** topic → get Topic ID

- [X] Remove `@RawDataBot` from group (optional, for privacy)

### 3.8 Update Infisical Secrets

Go back to Infisical and update:

- [X] `TELEGRAM_BOT_TOKEN` = your bot token
- [X] `TELEGRAM_GROUP_ID` = group ID (negative number)
- [X] `TELEGRAM_TOPIC_NOTES` = notes topic ID
- [X] `TELEGRAM_TOPIC_TASKS` = tasks topic ID
- [X] `TELEGRAM_TOPIC_SNIPPETS` = snippets topic ID

**Output**: Bot created, group configured, IDs in Infisical

---

## Step 4: Notion Setup

### 4.1 Create Integration

- [X] Go to [notion.so/my-integrations](https://www.notion.so/my-integrations)
- [X] Click "New integration"
- [X] Fill in:
    - Name: `DraftStream`
    - Logo: (optional)
    - Associated workspace: Select your workspace
- [X] Under Capabilities, ensure:
    - [x] Read content
    - [x] Insert content
    - [x] Update content
- [X] Click "Submit"
- [X] Copy the "Internal Integration Token" (format: `secret_xxxxx...`)

### 4.2 Update Infisical

- [X] Update `NOTION_INTEGRATION_TOKEN` in Infisical with the token

### 4.3 Create Notes Database

- [X] In Notion, create a new page
- [X] Add a Database - Full page
- [X] Name: `DraftStream Notes`
- [X] Add properties:

| Property Name | Type         | Configuration                                                    |
|---------------|--------------|------------------------------------------------------------------|
| Title         | Title        | (default, rename to "Title")                                     |
| Content       | Text         |                                                                  |
| Tags          | Multi-select | Add options: `idea`, `reminder`, `reference`, `personal`, `work` |
| Source        | Select       | Add option: `telegram`                                           |

### 4.4 Create Tasks Database

- [X] Create another Database - Full page
- [X] Name: `DraftStream Tasks`
- [X] Add properties:

| Property Name | Type         | Configuration                                |
|---------------|--------------|----------------------------------------------|
| Title         | Title        | (default)                                    |
| Description   | Text         |                                              |
| Status        | Status       | Groups: `Not started`, `In progress`, `Done` |
| Priority      | Select       | Options: `Low`, `Medium`, `High`, `Urgent`   |
| Project       | Select       | (leave empty for now)                        |
| Labels        | Multi-select | (leave empty for now)                        |
| Due Date      | Date         |                                              |

### 4.5 Create Snippets Database

- [X] Create another Database - Full page
- [X] Name: `DraftStream Snippets`
- [X] Add properties:

| Property Name | Type         | Configuration                                                                      |
|---------------|--------------|------------------------------------------------------------------------------------|
| Title         | Title        | (default)                                                                          |
| Code          | Text         |                                                                                    |
| Language      | Select       | Options: `bash`, `sql`, `csharp`, `docker`, `git`, `python`, `javascript`, `other` |
| Tags          | Multi-select | (leave empty for now)                                                              |
| Description   | Text         |                                                                                    |

### 4.6 Connect Integration to Databases

For **each** of the 3 databases:

- [X] Open the database page
- [X] Click `•••` (three dots menu) in top-right
- [X] Click "Connections" or "Add connections"
- [X] Find and select `DraftStream`
- [X] Confirm connection

### 4.7 Get Database IDs

For each database:

- [X] Open as full page
- [X] Look at browser URL: `https://notion.so/workspace/Database-Name-abc123...`
- [X] The ID is the 32-character string (copy everything after the last hyphen before any `?`)
- [X] Format it with dashes: `abc123de-f456-7890-abcd-ef1234567890`

**Tip**: You can also use Notion's "Copy link" and extract the ID from the URL.

### 4.8 Update Infisical with Database IDs

- [X] `NOTION_DATABASE_NOTES` = Notes database ID
- [X] `NOTION_DATABASE_TASKS` = Tasks database ID
- [X] `NOTION_DATABASE_SNIPPETS` = Snippets database ID

**Output**: Integration created, databases ready, IDs in Infisical

---

## Final Checklist

### Secrets in Infisical (all should be real values now)

| Secret                     | Status |
|----------------------------|--------|
| `OPENROUTER_API_KEY`       | ✓      |
| `TELEGRAM_BOT_TOKEN`       | ✓      |
| `TELEGRAM_GROUP_ID`        | ✓      |
| `TELEGRAM_TOPIC_NOTES`     | ✓      |
| `TELEGRAM_TOPIC_TASKS`     | ✓      |
| `TELEGRAM_TOPIC_SNIPPETS`  | ✓      |
| `NOTION_INTEGRATION_TOKEN` | ✓      |
| `NOTION_DATABASE_NOTES`    | ✓      |
| `NOTION_DATABASE_TASKS`    | ✓      |
| `NOTION_DATABASE_SNIPPETS` | ✓      |

### Service Credentials (save locally/securely)

| Item                    | Have it? |
|-------------------------|----------|
| Infisical URL           | ✓        |
| Infisical Service Token | ✓        |
| Infisical Project ID    | ✓        |

### External Services Ready

| Service                           | Status |
|-----------------------------------|--------|
| OpenRouter account + API key      | ✓      |
| Telegram bot created and in group | ✓      |
| Telegram group with 3 topics      | ✓      |
| Notion integration connected      | ✓      |
| Notion databases (3) with schemas | ✓      |

---

## Verification Tests

### Test 1: Telegram Bot Responds

Send `/start` to your bot in private chat. It should... do nothing (expected - no code yet). But you shouldn't see an
error.

### Test 2: Notion Integration Has Access

1. Go to one of your databases
2. Try adding a page manually
3. If you can, the integration should be able to as well

### Test 3: OpenRouter API Works

Run the curl command from Step 1.4 - should get a response.

---

## Next Step

Once all prerequisites are complete, proceed to **Phase 0: Foundation & Solution Scaffolding**.
