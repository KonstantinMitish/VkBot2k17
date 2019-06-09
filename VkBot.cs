using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using VkNet;
using VkNet.Enums;
using VkNet.Enums.Filters;
using VkNet.Enums.SafetyEnums;
using VkNet.Model;
using VkNet.Model.Attachments;
using VkNet.Model.RequestParams;

namespace VkBot2k17
{
  public class VkBot
  {
    public static Random Rnd = new Random();
    private const long AppId = 5180171;
    private const string AppSecret = "******************************";
    private VkApi api = new VkApi();

    private MessagesGetParams ParamsOut = new MessagesGetParams
    {
      Out = MessageType.Sended,
      Count = 10,
      Offset = 0
    };

    private MessagesGetParams ParamsIn = new MessagesGetParams
    {
      Out = MessageType.Received,
      Count = 10,
      Offset = 0
    };

    class Album
    {
      private List<Photo> Photos;

      private int LastPhoto;

      public Album( IReadOnlyCollection<Photo> NewPhotos )
      {
        Photos = new List<Photo>(NewPhotos);
        LastPhoto = Rnd.Next(Photos.Count);
      }

      public Photo Next()
      {
        LastPhoto++;
        if (LastPhoto >= Photos.Count)
          LastPhoto = 0;
        return Photos[LastPhoto];
      }
    }

    /* Albums w\ photos */
    private Dictionary<string, Album> Albums = new Dictionary<string, Album>();

    /* Commands w\ photos */
    private Dictionary<string, Action> Commands = new Dictionary<string, Action>();

    /* Working chats */
    private List<long> Chats = new List<long>();

    /* Banned users */
    private List<long> BlackList = new List<long>();

    /* json handles */

    #region json

    public class jAlbum
    {
      public string Name { get; set; }
      public long OwnerId { get; set; }
      public long AlbumId { get; set; }
    }

    public class jAlbums
    {
      public List<jAlbum> Albums { get; set; }
    }

    public class jCommand
    {
      public string Command { get; set; }
      public jAction Message { get; set; }
    }

    public class jCommnds
    {
      public List<jCommand> Commands { get; set; }
    }

    public class jAction
    {
      public string Text { get; set; }
      public string RandomAttachment { get; set; }
      public List<jAttachment> Attachments { get; set; }
    }

    public class jAttachment
    {
      public string Type { get; set; }
      public long Id { get; set; }
      public long OwnerId { get; set; }
    }

    #endregion

    /* Commands handles */

    public class Action
    {
      public string Text { get; set; }
      public List<MediaAttachment> Attachments { get; set; }
      public Func<Photo> RandomAttachment { get; set; }

      public Action()
      {
        Text = "";
      }

      public long Execute( VkBot bot, MessagesSendParams Params )
      {
        Params.Message = bot.Prefix + Text;
        Params.Attachments = Attachments;
        if (RandomAttachment != null)
        {
          List<MediaAttachment> attach = new List<MediaAttachment>();
          attach.Add(RandomAttachment());
          Params.Attachments = attach;
        }
        Thread.Sleep(500);
        return bot.api.Messages.Send(Params);
      }
    }

    private bool Sw = true;

    public string Prefix
    {
      get
      {
        Sw = !Sw;
        if (Sw)
          return "1337 Bot: ";
        else
          return "1337_Bot: ";
      }
    }

    private void Auth( string login, string password, Func<string> TwoFactor = null )
    {
      /* Authtorization */
      api.Authorize(new ApiAuthParams
      {
        ApplicationId = AppId,
        Login = login,
        Password = password,
        Settings = Settings.All,
        //TwoFactorAuthorization = TwoFactor,
      });
    }

    private void LoadAlbums( string FileName )
    {
      var res = (jAlbums) Newtonsoft.Json.JsonConvert.DeserializeObject(File.ReadAllText(FileName), typeof(jAlbums));

      foreach (jAlbum album in res.Albums)
      {
        var k = api.Photo.Get(new PhotoGetParams
        {
          OwnerId = album.OwnerId,
          AlbumId = PhotoAlbumType.Id(album.AlbumId)
        });
        Albums.Add(album.Name, new Album(k));
      }
    }

    private void LoadCommands( string FileName )
    {
      var res = (jCommnds) Newtonsoft.Json.JsonConvert.DeserializeObject(File.ReadAllText(FileName), typeof(jCommnds));

      foreach (jCommand command in res.Commands)
      {
        Action action = new Action();

        if (command.Message.Text == null)
          action.Text = "";
        else
          action.Text = command.Message.Text;
        if (command.Message.RandomAttachment != null)
        {
          action.RandomAttachment = Albums[command.Message.RandomAttachment].Next;
        }
        else
        if (command.Message.Attachments != null)
        {
          action.Attachments = new List<MediaAttachment>();
          foreach (var attachment in command.Message.Attachments)
          {
            switch (attachment.Type)
            {
              case "Photo":
                action.Attachments.Add(new Photo
                {
                  Id = attachment.Id,
                  OwnerId = attachment.OwnerId
                });
                break;
              case "Video":
                action.Attachments.Add(new Video
                {
                  Id = attachment.Id,
                  OwnerId = attachment.OwnerId
                });
                break;
              case "Audio":
                action.Attachments.Add(new Audio
                {
                  Id = attachment.Id,
                  OwnerId = attachment.OwnerId
                });
                break;
            }
          }
        }
        Commands.Add(command.Command, action);
      }
    }

    private MessagesGetObject GetIn()
    {
      var Messages = api.Messages.Get(ParamsIn);
      if (Messages.Messages.Count != 0)
        ParamsIn.LastMessageId = Messages.Messages.First().Id;
      return Messages;
    }

    private MessagesGetObject GetOut()
    {
      var Messages = api.Messages.Get(ParamsOut);
      if (Messages.Messages.Count != 0)
        ParamsOut.LastMessageId = Messages.Messages.First().Id;
      return Messages;
    }

    private void ProcessCommand( Message msg )
    {
      string text = msg.Body.ToLower();
      foreach (var command in Commands)
      {
        if (text.Contains(command.Key))
          ParamsOut.LastMessageId = command.Value.Execute(this, new MessagesSendParams
          {
            ChatId = msg.ChatId,
            UserId = msg.ChatId == null ? msg.UserId : null,
            PeerId = msg.ChatId != null ? 2000000000 + msg.ChatId : msg.UserId
          });
      }
    }

    public void Run()
    {
      /* Get last message ids */
      GetIn();
      Thread.Sleep(500);
      GetOut();
      Thread.Sleep(500);

      bool RunFlag = true;

      while (RunFlag)
      {
        var messages = GetOut();

        foreach (var message in messages.Messages)
        {
          long chat;
          if (message.ChatId == null)
            chat = message.UserId.Value;
          else
            chat = 2000000000 + message.ChatId.Value;
           
          if (Chats.Contains(chat))
            ProcessCommand(message);
        }

        Thread.Sleep(500);
        messages = GetIn();

        foreach (var message in messages.Messages)
        {
          long chat;
          if (message.ChatId == null)
            chat = message.UserId.Value;
          else
            chat = 2000000000 + message.ChatId.Value;

          #region low level commands

          if (message.UserId == 20046621 || message.UserId == 67073585)
          {
            if (message.Body.Contains("!старт"))
              if (!Chats.Contains(chat))
              {
                Chats.Add(chat);
                ParamsOut.LastMessageId = api.Messages.Send(new MessagesSendParams
                {
                  Message = Prefix + "Успешный запуск!",
                  ChatId = message.ChatId,
                  UserId = message.ChatId == null ? message.UserId : null,
                  PeerId = chat
                });
              }
            if (message.Body.Contains("!стоп"))
              if (Chats.Contains(chat))
              {
                Chats.Remove(chat);
                ParamsOut.LastMessageId = api.Messages.Send(new MessagesSendParams
                {
                  Message = Prefix + "Бот остановлен",
                  ChatId = message.ChatId,
                  UserId = message.ChatId == null ? message.UserId : null,
                  PeerId = chat
                });
              }

            if (message.Body.Contains("!бан"))
            {
              long id = -1;
              try
              {
                id = int.Parse(message.Body.Split(' ')[1]);
              }
              catch
              {
                try
                {
                  id = message.ForwardedMessages[0].UserId.Value;
                }
                catch
                {
                }
              }
              if (id == -1)
                ParamsOut.LastMessageId = api.Messages.Send(new MessagesSendParams
                {
                  Message = Prefix + "Не верно заданно имя",
                  ChatId = message.ChatId,
                  UserId = message.ChatId == null ? message.UserId : null,
                  PeerId = chat
                });
              else
              {
                ParamsOut.LastMessageId = api.Messages.Send(new MessagesSendParams
                {
                  Message = Prefix + "Пользователь id" + id + " игнорируется",
                  ChatId = message.ChatId,
                  UserId = message.ChatId == null ? message.UserId : null,
                  PeerId = chat
                });
                BlackList.Add(id);
              }
            }
            if (message.Body.Contains("!анбан"))
            {
              long id = -1;
              try
              {
                id = int.Parse(message.Body.Split(' ')[1]);
              }
              catch
              {
                try
                {
                  id = message.ForwardedMessages[0].UserId.Value;
                }
                catch
                {
                }
              }
              if (id == -1)
              {
                ParamsOut.LastMessageId = api.Messages.Send(new MessagesSendParams
                {
                  Message = Prefix + "Не верно заданно имя",
                  ChatId = message.ChatId,
                  UserId = message.ChatId == null ? message.UserId : null,
                  PeerId = chat
                });
              }
              else
              {
                try
                {
                  BlackList.Remove(id);
                  ParamsOut.LastMessageId = api.Messages.Send(new MessagesSendParams
                  {
                    Message = Prefix + "Пользователь id" + id + " исклюен из черного списка",
                    ChatId = message.ChatId,
                    UserId = message.ChatId == null ? message.UserId : null,
                    PeerId = chat
                  });
                }
                catch
                {
                  ParamsOut.LastMessageId = api.Messages.Send(new MessagesSendParams
                  {
                    Message = Prefix + "Пользователь id" + id + " не найден в черном списке.",
                    ChatId = message.ChatId,
                    UserId = message.ChatId == null ? message.UserId : null,
                    PeerId = chat
                  });
                }
              }
            }
          }
          #endregion
          if (Chats.Contains(chat))
            ProcessCommand(message);
        }
      }
    }

    /* Constructor */

    public VkBot( string login, string password, Func<string> TwoFactor = null, string AlbumsFile = "Albums.json",
      string CommandsFile = "Commands.json" )
    {
      try
      {
        Auth(login, password, TwoFactor);
        LoadAlbums(AlbumsFile);
        LoadCommands(CommandsFile);
      }
      catch (Exception e)
      {
        Console.WriteLine("VSE O4EN PLOHO");
        Console.WriteLine(e);
      }
    }
  }
}