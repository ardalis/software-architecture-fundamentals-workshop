# Exercise 0: Set Up Your Machine

**Module:** Welcome and Setup (2-day module 1, 1-day module 1)
**Format:** on your own
**Time:** 30–45 min **before the workshop**, mostly waiting for downloads. 5 min in the room to check that it all works.

## Goal

Have everything the exercises need working before you need it, so the workshop time goes to architecture, not installers. The downloads are big (the .NET SDK, container images, NuGet packages), and venue Wi-Fi is slow, so do this at home or at the office if you can.

## Steps

1. **Install the .NET 10 SDK** from [dot.net](https://dotnet.microsoft.com/download). Check it:

   ```sh
   dotnet --version    # 10.0.100 or later
   ```

2. **Install Docker Desktop** (or Podman) and start it. Check it:

   ```sh
   docker info
   ```

3. **Install the Aspire CLI**, which runs the samples and their containers. Follow the [install instructions](https://aspire.dev/get-started/install-cli/) for your OS, then check it:

   ```sh
   aspire --version
   ```

4. **Clone the workshop repository** and open it in your editor (Visual Studio, VS Code, or Rider):

   ```sh
   git clone https://github.com/ardalis/software-architecture-fundamentals-workshop.git
   cd software-architecture-fundamentals-workshop
   ```

5. **Trust the .NET development certificate**, so the Aspire dashboard opens over HTTPS. You only ever need to do this once per machine:

   ```sh
   dotnet dev-certs https --trust
   ```

6. **Run a code exercise.** The first run restores packages:

   ```sh
   cd exercises/coupling-cohesion
   dotnet run OrderManager.cs
   cd ../..
   ```

   You should see an order placed and a settlement report.

7. **Build and test a sample.** This downloads the PostgreSQL and Papercut container images and runs the sample's tests against them. It takes a few minutes the first time:

   ```sh
   dotnet BuildTestFormat.cs clean-architecture
   ```

   It should end with `All steps passed.`

8. **Two-day workshop only: get the message broker too.** The asynchronous saga samples also use RabbitMQ:

   ```sh
   dotnet BuildTestFormat.cs anthology-saga
   ```

9. **Get an AI coding agent ready**, such as Claude Code or GitHub Copilot, signed in and able to read the repository. Ask it "What samples are in this repository, and how do I run one?" and check that the answer matches the README.

## Done when

| Check | You see |
| --- | --- |
| `dotnet --version` | 10.0.100 or later |
| `docker info` | Server details, not an error |
| `aspire --version` | A version number |
| `dotnet run OrderManager.cs` | A settlement report |
| `dotnet BuildTestFormat.cs clean-architecture` | `All steps passed.` |
| Your AI agent | A sensible answer about the repository |

## If something doesn't work

- **`docker info` fails, or the tests can't start containers:** start Docker Desktop and wait until it says it's running. On Windows, Docker Desktop needs WSL 2.
- **Package or image downloads fail on a corporate network:** you may need your proxy settings in Docker Desktop and in the `HTTP_PROXY` / `HTTPS_PROXY` environment variables. Try a different network if you can.
- **The tests time out the first time:** the image downloads may have taken too long. Run the same command again.
- **Port 5240 is already in use:** another sample is still running. Stop it with Ctrl+C. The samples share ports, so run one at a time.

If you're stuck, bring it to the workshop. We'll check everyone's setup at the start, and you can pair with someone whose setup works.
