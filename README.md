# /r/SAO Moderation Bot

[!screenshot](https://i.imgur.com/Ok0Bu9X.png)

This is a Reddit bot written in C# and .NET Core, built to assist in moderating /r/SwordArtOnline. 

## Getting Started

- Copy `config.dist.json` to `config.json` and populate it with the required parameters. Make sure `config.json` is set to copy to the output directory on each build.
- Build and run the project.

## Built With

* .NET Core 
* [RedditSharp](https://github.com/CrustyJew/RedditSharp) - Reddit API client
* [AutoFac](https://github.com/autofac/Autofac) - IoC container
* [Serilog](https://github.com/serilog/serilog) - Logging library

## Authors

* **OmegaVesko** - omegavesko@gmail.com

## License

This project is licensed under the GPLv3 License - see the [LICENSE.md](LICENSE.md) file for details
