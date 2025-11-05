# Discord.NET Bot Template
This is a template for making C# Discord bots with the Discord.NET framework in a containerized fashion with Docker.

# Wanna Go Fast?
If you aren't interested in self hosting, and you just wanna get your bot online, click this big purple button to deploy to the cloud instantly!

[![Deploy on Railway](https://railway.app/button.svg)](https://railway.app/template/inw1EU?referralCode=FBRIfP)



# Discord Bot Setup Guide

This guide will walk you through setting up a simple Discord bot using Discord.net, either by running it locally using Docker or deploying it to Railway.

## Prerequisites

Before starting, make sure you have the following installed:

- [.NET SDK](https://dotnet.microsoft.com/download) (version 6.0 or later)
- [Docker](https://www.docker.com/get-started) (for local setup)
- A [Discord account](https://discord.com/) with permissions to create a bot
- A [Railway](https://railway.app/) account (for cloud deployment)

## Step 1: Clone the Repository

Start by cloning the bot's repository to your local machine.

```bash
git clone https://github.com/your-repo/discord-bot.git
cd discord-bot
```

## Step 2: Set Up the Environment Variables

Create a `.env` file in the root directory of your project and add the following content:

```env
DISCORD_TOKEN=your-discord-bot-token
```

Replace `your-discord-bot-token` with your actual bot token. You can obtain this token from the [Discord Developer Portal](https://discord.com/developers/applications).

## Step 3: Use the Dockerfile

Here’s the Dockerfile you provided:

```dockerfile
# Use the official .NET image as a base image
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build

# Set the working directory to the current directory
WORKDIR /app

# Copy the project file and restore dependencies
COPY *.csproj ./
RUN dotnet restore

# Copy the rest of the application
COPY . ./

# Build the application
RUN dotnet publish -c Release -o out

# Use a runtime image
FROM mcr.microsoft.com/dotnet/runtime:6.0

# Set the working directory to the current directory
WORKDIR /app

# Copy the built application from the build stage
COPY --from=build /app/out .

# Set the entry point
ENTRYPOINT ["dotnet", "main.dll"]
```

Ensure that `main.dll` matches the actual name of your bot's compiled `.dll` file.

## Option 1: Running Locally with Docker

### Step 4: Build and Run the Docker Container

Now, build and start the Docker container locally.

```bash
docker build -t discord-bot .
docker run -d --name discord-bot --env-file .env discord-bot
```

This command will build the Docker image and run the bot in a container. The bot will continue running in the background.

### Step 5: Inviting Your Bot to a Server

1. Go to the [Discord Developer Portal](https://discord.com/developers/applications) and select your bot.
2. Under the "OAuth2" tab, go to the "URL Generator".
3. Under "OAuth2 Scopes", check the `bot` box.
4. Under "Bot Permissions", select the necessary permissions for your bot.
5. Copy the generated URL and paste it into your browser.
6. Select the server you want to add the bot to and authorize it.

### Step 6: Test Your Bot

Once the bot is running, you can test it by typing the following commands in your Discord server:

- `/ping` - Check the bot's latency.
- `/hi @username` - Say hi to a specific user.
- `/random coin-toss` - Flip a coin.
- `/random dice-roll` - Roll a 6-sided die.

### Additional Notes

- **Logs**: You can view the logs from the bot container using the following command:
  ```bash
  docker logs discord-bot
  ```

- **Stopping the Bot**: To stop the bot, run:
  ```bash
  docker stop discord-bot
  docker rm discord-bot
  ```

## Option 2: Deploying to Railway

### Step 4: Deploy to Railway

1. Log in to your [Railway account](https://railway.app/).
2. Create a new project and select "Deploy from GitHub repository."
3. Connect your GitHub account and select the bot's repository.
4. Set up environment variables on Railway. Go to your project settings and add the following environment variable:

   - `DISCORD_TOKEN=your-discord-bot-token`

5. Railway will automatically detect the Dockerfile and deploy the bot.

### Step 5: Inviting Your Bot to a Server

Follow the same steps as in the local setup to invite your bot to a server.

### Step 6: Monitor and Manage Your Bot

Railway provides logs and management tools to monitor your bot's performance and status.

- **Logs**: You can view logs from the Railway dashboard.
- **Scaling**: Adjust the resources allocated to your bot if necessary.