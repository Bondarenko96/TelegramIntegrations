using System.ComponentModel;

namespace Bot;

public enum BotCommands
{
  [Description("/choose")]
  Choose,
  [Description("/formula")]
  Formula,
  [Description("/gpt")]
  SendToGpt,
  [Description("/imagine")]
  Imagine,
  [Description("/post")]
  PostToPublic,
  [Description("/stopImagine")]
  StopImagine,
  [Description("/test")]
  Test,
}


