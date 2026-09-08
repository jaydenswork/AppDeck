using System;
using System.Collections.Generic;
using System.Drawing;
using System.Text.RegularExpressions;

namespace AppDeck {
    internal static class DisplayTasks {
        public static IEnumerable<int> NeedingResize(string text,int displayId,Size size){
            if(displayId<=0)yield break;
            bool owned=false;var seen=new HashSet<int>();
            foreach(string line in text.Split('\n')){
                var root=Regex.Match(line,@"^RootTask id=\d+ .*\bdisplayId=(\d+)\b");
                if(root.Success){owned=Int32.Parse(root.Groups[1].Value)==displayId;continue;}
                if(!owned)continue;
                var task=Regex.Match(line,@"^\s+taskId=(\d+): .*bounds=\[(-?\d+),(-?\d+)\]\[(-?\d+),(-?\d+)\].*\bvisible=true\b");
                if(!task.Success)continue;
                int id=Int32.Parse(task.Groups[1].Value);
                if((task.Groups[2].Value!="0"||task.Groups[3].Value!="0"||Int32.Parse(task.Groups[4].Value)!=size.Width||Int32.Parse(task.Groups[5].Value)!=size.Height)&&seen.Add(id))yield return id;
            }
        }
    }
}
