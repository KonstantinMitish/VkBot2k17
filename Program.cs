using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace VkBot2k17
{
  class Program
  {
    static void Main( string[] args )
    {
      VkBot bot = new VkBot("login", "******************************");
      while (true)
      {
        try
        {                      
          bot.Run();
        }
        catch (Exception e)
        {
          Console.WriteLine(e);
        }
      }
    }
  }
}
