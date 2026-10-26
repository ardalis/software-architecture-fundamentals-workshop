# Software Architecture Fundamentals Workshop

Exercises, runnable samples, and animated diagrams for the **Software Architecture Fundamentals** workshop by Steve "ardalis" Smith. This copy is for **Techorama NL 2026**.

| Folder | What's in it |
| --- | --- |
| [`exercises/`](./exercises/) | The workshop's exercises and discussions, the TicketTide case study, worksheets, templates, and starter code |
| [`samples/`](./samples/) | Runnable .NET 10 + Aspire samples: Clean Architecture, Vertical Slice Architecture, an Atomic Modular Monolith, and one for each of the eight saga patterns |
| [`diagrams/`](./diagrams/) | Animated diagrams from the slides, which you can step through at your own pace |

## What you need

Work through [Exercise 0: set up your machine](./exercises/setup.md) **before the workshop**. It installs and checks everything below, and downloads the container images ahead of time.

- [.NET 10 SDK](https://dotnet.microsoft.com/download) for the code exercises and the samples
- Docker Desktop (or Podman), running, for the samples. They start PostgreSQL, SQL Server, RabbitMQ, and Papercut containers.
- The [Aspire CLI](https://aspire.dev/get-started/install-cli/), to run the samples
- An internet connection. The samples download packages and container images the first time, and the diagrams load their animation engine from a CDN.
- An editor: Visual Studio, VS Code, or Rider

## Exercises

Start with the [exercises index](./exercises/README.md). It lists every exercise in the order the workshop runs them. Most use **TicketTide**, a fictional ticketing company described in the [case study brief](./exercises/tickettide/brief.md).

The code exercises are .NET 10 file-based apps: a single `.cs` file with no project file. Run one from its folder:

```sh
cd exercises/fitness-functions
dotnet run fitness.cs
```

## Samples

See the [samples README](./samples/README.md) for what each sample shows and how to run it. In short:

```sh
cd samples/epic-saga
dotnet run --project src/TicketTide.AppHost
```

The Aspire dashboard uses HTTPS. If you've never trusted the .NET development certificate on your machine, run `dotnet dev-certs https --trust` once, or start the sample with `--launch-profile http` added to the command.

When the `api` resource in the Aspire dashboard is healthy, open `http://localhost:5240`. A saga sample's home page has a button for each scenario and a **See the diagram** button that plays that saga's animated diagram. Run one sample at a time, because they all use the same ports.

To build and test every sample at once (the tests need Docker), run this from the repository root:

```sh
dotnet BuildTestFormat.cs
```

## Diagrams

The diagram pages use JavaScript modules, which browsers don't load from files opened directly from disk. Serve the folder with any local web server, then open the address it prints. Pick whichever of these you have installed:

```sh
npx serve diagrams                          # Node.js
python3 -m http.server 8000 -d diagrams     # Python (python on Windows), then open http://localhost:8000
dotnet tool install -g dotnet-serve         # .NET, once
dotnet serve -d diagrams -o                 # .NET
```

Space plays or pauses a diagram, and the Right Arrow key steps through it.
