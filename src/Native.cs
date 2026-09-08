using System;
using System.Runtime.InteropServices;
using System.Text;
namespace AppDeck {
    internal static class Native {
        [DllImport("user32.dll",SetLastError=true)] internal static extern bool RegisterHotKey(IntPtr h,int id,uint modifiers,uint key);
        [DllImport("user32.dll")] internal static extern bool UnregisterHotKey(IntPtr h,int id);
        [DllImport("user32.dll")] internal static extern IntPtr GetForegroundWindow();
        [DllImport("user32.dll")] internal static extern IntPtr GetAncestor(IntPtr h,uint flags);
        internal delegate bool EnumProc(IntPtr h, IntPtr p);
        [DllImport("user32.dll")] static extern bool EnumWindows(EnumProc proc, IntPtr p);
        [DllImport("user32.dll")] internal static extern uint GetWindowThreadProcessId(IntPtr h, out uint pid);
        [DllImport("user32.dll", CharSet=CharSet.Unicode)] static extern int GetClassName(IntPtr h, StringBuilder s, int n);
        [DllImport("user32.dll", EntryPoint="GetWindowLongPtrW")] static extern IntPtr GetStyle(IntPtr h, int index);
        [DllImport("user32.dll", EntryPoint="SetWindowLongPtrW", SetLastError=true)] static extern IntPtr SetStyle(IntPtr h, int index, IntPtr value);
        [DllImport("user32.dll", SetLastError=true)] internal static extern IntPtr SetParent(IntPtr h, IntPtr parent);
        [DllImport("user32.dll")] internal static extern bool IsWindow(IntPtr h);
        [DllImport("user32.dll")] internal static extern bool IsWindowVisible(IntPtr h);
        [DllImport("user32.dll")] internal static extern IntPtr GetParent(IntPtr h);
        [DllImport("user32.dll")] internal static extern bool MoveWindow(IntPtr h,int x,int y,int w,int height,bool repaint);
        [DllImport("user32.dll")] internal static extern bool SetWindowPos(IntPtr h,IntPtr after,int x,int y,int cx,int cy,uint flags);
        [DllImport("user32.dll")] internal static extern IntPtr SetFocus(IntPtr h);
        [DllImport("user32.dll")] internal static extern bool PostMessage(IntPtr h,uint msg,IntPtr w,IntPtr l);
        [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr h,int cmd);
        internal static IntPtr FindScrcpy(int pid) {
            IntPtr result=IntPtr.Zero;
            EnumWindows((h,p)=> { uint id; GetWindowThreadProcessId(h,out id); if(id!=pid)return true;
                var s=new StringBuilder(128); GetClassName(h,s,s.Capacity);
                if(s.ToString().StartsWith("SDL")) { result=h; return false; } return true;
            },IntPtr.Zero); return result;
        }
        internal static void Embed(IntPtr child, IntPtr parent) {
            long style=GetStyle(child,-16).ToInt64();
            style &= ~unchecked((long)0x80CF0000); // popup, caption, sizing frame, system menu
            style |= 0x50000000; // child, visible
            SetStyle(child,-16,new IntPtr(style));
            SetParent(child,parent);
            if(GetParent(child)!=parent) throw new InvalidOperationException("无法嵌入投屏窗口，请使用独立窗口模式。");
            SetWindowPos(child,IntPtr.Zero,0,0,0,0,0x0037); ShowWindow(child,5);
        }
        internal static void Detach(IntPtr child) {
            SetParent(child,IntPtr.Zero);
            long style=GetStyle(child,-16).ToInt64();style &= ~0x40000000L;style |= 0x10CF0000L;
            SetStyle(child,-16,new IntPtr(style));SetWindowPos(child,IntPtr.Zero,0,0,0,0,0x0037);ShowWindow(child,5);
        }
    }
}
