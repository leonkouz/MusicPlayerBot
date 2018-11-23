﻿using Discord;
using Discord.Audio;
using Discord.Commands;
using Discord.WebSocket;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace MusicBotPlayer
{
    public class DiscordBot
    {
        /// <summary>
        /// Specifies if the bot is connected to a voice channel.
        /// </summary>
        public static bool IsConnectedToVoiceChannel { get; set; } = false;

        /// <summary>
        /// The currently connected voice channel.
        /// </summary>
        public static IVoiceChannel ConnectedChannel { get; set; } = null;

        /// <summary>
        /// The Audio Channel.
        /// </summary>
        public static IAudioClient AudioClient { get; set; } = null;

        private static CommandService commands;
        private static IServiceProvider services;
        private static DiscordSocketClient client;

        /// <summary>
        /// Initialise the bot and login.
        /// </summary>
        public static void InitialiseBot()
        {
            Initialise().GetAwaiter().GetResult();
        }

        /// <summary>
        /// Run initialisation process.
        /// </summary>
        /// <returns></returns>
        private static async Task Initialise()
        {
            client = new DiscordSocketClient();
            commands = new CommandService();

            services = new ServiceCollection()
                .BuildServiceProvider();

            await InstallCommands();

            string token = Startup.GetDiscordBotTokenFromAppData();
            await client.LoginAsync(TokenType.Bot, token);
            await client.StartAsync();

            client.MessageReceived += MessageReceived;

            // Block this task until the program is closed.
            await Task.Delay(-1);
        }

        /// <summary>
        /// Install the command module.
        /// </summary>
        /// <returns></returns>
        public static async Task InstallCommands()
        {
            // Hook the MessageReceived Event into our Command Handler
            client.MessageReceived += HandleCommand;
            // Discover all of the commands in this assembly and load them.
            await commands.AddModulesAsync(Assembly.GetEntryAssembly());
        }

        /// <summary>
        /// Handles commands.
        /// </summary>
        /// <param name="messageParam"></param>
        /// <returns></returns>
        public static async Task HandleCommand(SocketMessage messageParam)
        {
            // Don't process the command if it was a System Message
            var message = messageParam as SocketUserMessage;
            if (message == null) return;
            // Create a number to track where the prefix ends and the command begins
            int argPos = 0;
            // Determine if the message is a command, based on if it starts with '!' or a mention prefix
            if (!(message.HasCharPrefix('!', ref argPos) || message.HasMentionPrefix(client.CurrentUser, ref argPos))) return;
            // Create a Command Context
            var context = new CommandContext(client, message);
            // Execute the command. (result does not indicate a return value, 
            // rather an object stating if the command executed successfully)
            var result = await commands.ExecuteAsync(context, argPos, services);
            if (!result.IsSuccess)
                await context.Channel.SendMessageAsync(result.ErrorReason);
        }

        /// <summary>
        /// Fires when a message is received in Discord.
        /// </summary>
        /// <param name="message">The message received.</param>
        /// <returns></returns>
        private static async Task MessageReceived(SocketMessage message)
        {
        }

        /// <summary>
        /// Initialise the FFMPEG stream.
        /// </summary>
        /// <param name="songPath">The path to the song.</param>
        /// <returns></returns>
        private static Process InitialiseAudioStream(string songPath)
        {
            var process = Process.Start(new ProcessStartInfo
            {
                // FFmpeg requires us to spawn a process and hook into its stdout, so we will create a Process
                FileName = @"C:\Users\Leon\Source\Repos\MusicBotPlayer\MusicBotPlayer\ffmpeg.exe",
                Arguments = $"-i {songPath} " + // Here we provide a list of arguments to feed into FFmpeg. -i means the location of the file/URL it will read from
                           "-f s16le -stats -ar 48000 -ac 2 pipe:1", // Next, we tell it to output 16-bit 48000Hz PCM, over 2 channels, to stdout.
                UseShellExecute = false,
                RedirectStandardOutput = true // Capture the stdout of the process
                
            });

            return process;
        }

        /// <summary>
        /// Sends audio to the discord voice channel.
        /// </summary>
        /// <param name="path"></param>
        /// <returns></returns>
        public static async Task SendAudioToVoice(string path)
        {
            var ffmpeg = InitialiseAudioStream(@"D:\Downloads\DarudeSandstorm.mp3");
            var output = ffmpeg.StandardOutput.BaseStream;
            var discord = AudioClient.CreatePCMStream(AudioApplication.Mixed);
            await output.CopyToAsync(discord);
            await discord.FlushAsync();
        }
    }
}
