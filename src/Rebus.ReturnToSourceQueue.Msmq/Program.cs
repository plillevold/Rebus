﻿using System;
using System.Collections.Generic;
using System.Messaging;
using System.Transactions;
using Rebus.Bus;
using Rebus.Logging;
using Rebus.Shared;
using Rebus.Transports.Msmq;
using System.Linq;

namespace Rebus.ReturnToSourceQueue.Msmq
{
    class Program
    {
        static int Main(string[] args)
        {
            RebusLoggerFactory.Current = new NullLoggerFactory();

            try
            {
                var parameters = PromptArgs(ParseArgs(args));

                Run(parameters);

                return 0;
            }
            catch (NiceException e)
            {
                Print(e.Message);

                return -1;
            }
            catch (Exception e)
            {
                Print(e.ToString());

                return -2;
            }
        }

        static Parameters PromptArgs(Parameters parameters)
        {
            if (!parameters.DryRun.HasValue && parameters.Interactive)
            {
                var dryrun = PromptChar(new[] {'d', 'm'}, "Perform a (d)ry dun or actually (m) move messages?");

                switch (dryrun)
                {
                    case 'd':
                        parameters.DryRun = true;
                        break;
                    case 'm':
                        parameters.DryRun = false;
                        break;
                    default:
                        throw new NiceException(
                            "Invalid option: {0} - please type (d) to perform a dry run (i.e. don't actually move anything), or (m) to actually move the messages");
                }
            }

            if (string.IsNullOrWhiteSpace(parameters.ErrorQueueName))
            {
                var errorQueueName = Prompt("Please type the name of an error queue");
                
                parameters.ErrorQueueName = errorQueueName;
            }

            if (!parameters.AutoMoveAllMessages.HasValue)
            {
                var mode = PromptChar(new[] {'a', 'p'},
                                      "Move (a)ll messages back to their source queues or (p)rompt for each message");

                switch (mode)
                {
                    case 'a':
                        parameters.AutoMoveAllMessages = true;
                        break;
                    case 'p':
                        parameters.AutoMoveAllMessages = false;
                        break;
                    default:
                        throw new NiceException("Invalid option: {0} - please type (a) to move all the messages, or (p) to be prompted for each message", mode);
                }
            }


            return parameters;
        }

        static Parameters ParseArgs(string[] args)
        {
            if (args.Length == 0) return new Parameters {Interactive = true};

            var parameters = new Parameters { Interactive = false };

            if (args.Any(a => a.Contains('?')))
            {
                throw HelpException();
            }

            var argsNotFlags = args.Where(a => !a.StartsWith("--")).ToList();

            if (argsNotFlags.Count > 1)
            {
                throw new NiceException("Invalid number of unnamed args: {0}", string.Join(" ", argsNotFlags));
            }

            parameters.ErrorQueueName = argsNotFlags.FirstOrDefault();

            var theRestOfTheArguments = args.Except(argsNotFlags)
                                            .Select(a => a.ToLowerInvariant())
                                            .ToArray();

            var validArgs = new Dictionary<string, Action<Parameters>> 
                                {
                                    {"--auto-move", p => p.AutoMoveAllMessages = true},
                                    {"--dry", p => p.DryRun = true}
                                };

            foreach (var arg in theRestOfTheArguments)
            {
                if (!validArgs.Keys.Contains(arg))
                {
                    throw new NiceException("Unknown argument: {0} - invoke with ? to get help", arg);
                }

                // apply the argument
                validArgs[arg](parameters);
            }

            return parameters;
        }

        static NiceException HelpException()
        {
            return new NiceException(@"Rebus Return To Source Queue Tool - MSMQ flavor    (-■_■)

Invoke without any arguments

    returnToSourceQueue.msmq.exe

to be prompted for each option. Or invoke with the following arguments:

    returnToSourceQueue.msmq.exe <errorQueueName> [--auto-move] [--dry]

where the following options are available:

    --auto-move :   Will quickly run through all message, moving those that can be moved
    --dry       :   Will SIMULATE running, i.e. no messages will actually be moved

e.g. like this:

    returnToSourceQueue.msmq myErrorQueue

in order to start processing the messages from 'myErrorQueue', or

    returnToSourceQueue.msmq myErrorQueue --auto-move

in order to automatically retry all messages that have the '{0}' header set, or

    returnToSourceQueue.msmq myErrorQueue --auto-move --dry

in order to SIMULATE automatically processing all messages (queue transaction will be aborted).", Headers.SourceQueue);
        }

        static string Prompt(string question, params object[] objs)
        {
            Console.Write(question, objs);
            Console.Write(" > ");
            return Console.ReadLine();
        }

        static char PromptChar(char[] validChars, string question, params object[] objs)
        {
            Console.Write(question, objs);
            Console.Write(" ({0}) > ", string.Join("/", validChars));
            char charToReturn;

            do
            {
                charToReturn = char.ToLowerInvariant(Console.ReadKey(true).KeyChar);
            } while (!validChars.Select(char.ToLowerInvariant).Contains(charToReturn));

            Console.WriteLine(charToReturn);

            return charToReturn;
        }

        static void Print(string message, params object[] objs)
        {
            Console.WriteLine(message, objs);
        }

        class Parameters
        {
            public bool Interactive { get; set; }

            public string ErrorQueueName { get; set; }

            public bool? AutoMoveAllMessages { get; set; }

            public bool? DryRun { get; set; }
        }

        class NiceException : ApplicationException
        {
            public NiceException(string message, params object[] objs)
                : base(string.Format(message, objs))
            {
            }
        }

        static void Run(Parameters parameters)
        {
            if (string.IsNullOrWhiteSpace(parameters.ErrorQueueName))
            {
                throw new NiceException("Please specify the name of an error queue");
            }

            using (var tx = new TransactionScope())
            {
                var transactionContext = new AmbientTransactionContext();

                if (!MessageQueue.Exists(MsmqUtil.GetPath(parameters.ErrorQueueName)))
                {
                    throw new NiceException("The MSMQ queue '{0}' does not exist!", parameters.ErrorQueueName);
                }

                var msmqMessageQueue = new MsmqMessageQueue(parameters.ErrorQueueName, allowRemoteQueue: true);
                var allTheMessages = GetAllTheMessages(msmqMessageQueue, transactionContext);

                foreach (var message in allTheMessages)
                {
                    var transportMessageToSend = message.ToForwardableMessage();

                    try
                    {
                        if (!transportMessageToSend.Headers.ContainsKey(Headers.SourceQueue))
                        {
                            throw new NiceException(
                                "Message {0} does not have a source queue header - it will be moved back to the input queue",
                                message.Id);
                        }

                        var sourceQueue = (string)transportMessageToSend.Headers[Headers.SourceQueue];

                        if (parameters.AutoMoveAllMessages.GetValueOrDefault())
                        {
                            msmqMessageQueue.Send(sourceQueue, transportMessageToSend, transactionContext);

                            Print("Moved {0} to {1}", message.Id, sourceQueue);
                        }
                        else
                        {
                            var answer = PromptChar(new[] { 'y', 'n' }, "Would you like to move {0} to {1}? (y/n)",
                                                    message.Id, sourceQueue);

                            if (answer == 'y')
                            {
                                msmqMessageQueue.Send(sourceQueue, transportMessageToSend, transactionContext);

                                Print("Moved {0} to {1}", message.Id, sourceQueue);
                            }
                            else
                            {
                                msmqMessageQueue.Send(msmqMessageQueue.InputQueueAddress,
                                                      transportMessageToSend,
                                                      transactionContext);

                                Print("Moved {0} to {1}", message.Id, msmqMessageQueue.InputQueueAddress);
                            }
                        }
                    }
                    catch (NiceException e)
                    {
                        Print(e.Message);

                        msmqMessageQueue.Send(msmqMessageQueue.InputQueueAddress,
                                              transportMessageToSend,
                                              transactionContext);
                    }
                }

                if (parameters.DryRun.GetValueOrDefault())
                {
                    Print("Aborting queue transaction");
                    return;
                }
                
                if (!parameters.Interactive)
                {
                    tx.Complete();
                    return;
                }

                var commitAnswer = PromptChar(new[] {'y', 'n'}, "Would you like to commit the queue transaction?");

                if (commitAnswer == 'y')
                {
                    Print("Committing queue transaction");

                    tx.Complete();
                    return;
                }

                Print("Queue transaction aborted");
            }
        }

        static List<ReceivedTransportMessage> GetAllTheMessages(MsmqMessageQueue msmqMessageQueue, ITransactionContext transactionContext)
        {
            var messages = new List<ReceivedTransportMessage>();
            ReceivedTransportMessage transportMessage;

            while ((transportMessage = msmqMessageQueue.ReceiveMessage(transactionContext)) != null)
            {
                messages.Add(transportMessage);
            }

            return messages;
        }
    }
}
