import java.io.*;
import java.lang.reflect.*;
import java.util.*;
import java.util.concurrent.TimeUnit;

/** A task lease: move an existing task, then return it below the phone's current foreground. */
public final class TaskSession {
    static Class<?> atm, token, rect, txType, organizerType;
    static Object service;
    static File stateFile;
    static Properties state;

    static String boot()throws Exception{try(BufferedReader in=new BufferedReader(new FileReader("/proc/sys/kernel/random/boot_id"))){return in.readLine();}}

    static Object field(Object o,String name)throws Exception{return o.getClass().getField(name).get(o);}
    static int number(Object o,String name)throws Exception{return ((Number)field(o,name)).intValue();}
    static boolean flag(Object o,String name)throws Exception{return (Boolean)field(o,name);}
    static int value(String key,int fallback){return Integer.parseInt(state.getProperty(key,Integer.toString(fallback)));}
    static List<?> roots()throws Exception{return (List<?>)atm.getMethod("getAllRootTaskInfos").invoke(service);}
    static Object root(int id)throws Exception{for(Object task:roots())if(number(task,"taskId")==id)return task;return null;}
    static Object windowConfig(Object task)throws Exception{Object config=field(task,"configuration");return field(config,"windowConfiguration");}
    static int mode(Object task)throws Exception{Object wc=windowConfig(task);return (Integer)wc.getClass().getMethod("getWindowingMode").invoke(wc);}
    static String componentPackage(Object component)throws Exception{return component==null?"":(String)component.getClass().getMethod("getPackageName").invoke(component);}
    static String packageName(Object task)throws Exception{
        String name=componentPackage(field(task,"realActivity"));
        return name.length()>0?name:componentPackage(field(task,"baseActivity"));
    }
    static boolean matches(Object task,String pkg,int user)throws Exception{return number(task,"userId")==user&&packageName(task).equals(pkg);}
    static long priority(Object task)throws Exception{return ((Number)field(task,"lastActiveTime")).longValue()+(flag(task,"isVisible")?1L<<50:0);}
    static Object displayInfo(int display)throws Exception{
        Class<?> c=Class.forName("android.hardware.display.DisplayManagerGlobal");
        return c.getMethod("getDisplayInfo",int.class).invoke(c.getMethod("getInstance").invoke(null),display);
    }
    static boolean sameDisplay()throws Exception{
        Object info=displayInfo(value("display",-1));
        return info!=null&&state.getProperty("displayUnique","").equals(field(info,"uniqueId"));
    }
    static void save()throws Exception{
        File temp=new File(stateFile.getPath()+".tmp");
        try(FileOutputStream out=new FileOutputStream(temp)){state.store(out,"AppDeck task lease");out.getFD().sync();}
        if(!temp.renameTo(stateFile))throw new IOException("Cannot save task lease");
    }
    static void remember(Object task)throws Exception{
        int id=number(task,"taskId");String prefix="task."+id+".";
        if(state.containsKey(prefix+"package"))return;
        state.setProperty(prefix+"package",packageName(task));state.setProperty(prefix+"user",Integer.toString(number(task,"userId")));
        state.setProperty(prefix+"mode",Integer.toString(mode(task)));
        Object wc=windowConfig(task),bounds=wc.getClass().getMethod("getBounds").invoke(wc);
        for(String edge:new String[]{"left","top","right","bottom"})state.setProperty(prefix+edge,Integer.toString(number(bounds,edge)));
    }
    static void restoreConfig(Object tx,Object task)throws Exception{
        String prefix="task."+number(task,"taskId")+".";
        boolean original=state.getProperty(prefix+"package","").equals(packageName(task))&&value(prefix+"user",-1)==number(task,"userId");
        int oldMode=original?value(prefix+"mode",1):1;
        if(oldMode!=5)oldMode=1; // Only standalone fullscreen/freeform tasks are leased.
        Object bounds=null;
        if(oldMode==5)bounds=rect.getConstructor(int.class,int.class,int.class,int.class).newInstance(value(prefix+"left",0),value(prefix+"top",0),value(prefix+"right",0),value(prefix+"bottom",0));
        Object tok=field(task,"token");
        txType.getMethod("setWindowingMode",token,int.class).invoke(tx,tok,oldMode);
        txType.getMethod("setBounds",token,rect).invoke(tx,tok,bounds);
    }
    static void apply(Object tx)throws Exception{
        Class<?> c=Class.forName("android.window.WindowOrganizer");c.getMethod("applyTransaction",txType).invoke(c.getConstructor().newInstance(),tx);
    }
    static Object recent(String pkg,int user)throws Exception{
        Object slice=atm.getMethod("getRecentTasks",int.class,int.class,int.class).invoke(service,200,1,user);
        List<?> list=(List<?>)slice.getClass().getMethod("getList").invoke(slice);
        for(Object t:list)if(matches(t,pkg,user))return t;
        return null;
    }
    static Object options(int display)throws Exception{
        Class<?> c=Class.forName("android.app.ActivityOptions");Object options=c.getMethod("makeBasic").invoke(null);
        c.getMethod("setLaunchDisplayId",int.class).invoke(options,display);
        c.getMethod("setLaunchWindowingMode",int.class).invoke(options,5);
        return c.getMethod("toBundle").invoke(options);
    }
    static Object createBridge(int display,int mode)throws Exception{
        Object organizer=organizerType.getConstructor().newInstance(),cookie=Class.forName("android.os.Binder").getConstructor().newInstance();
        organizerType.getMethod("createRootTask",int.class,int.class,Class.forName("android.os.IBinder"),boolean.class).invoke(organizer,display,mode,cookie,false);
        for(Object task:roots())if(((List<?>)field(task,"launchCookies")).contains(cookie))return task;
        throw new IllegalStateException("Cannot create transfer container");
    }
    static void cleanEntryBridge()throws Exception{
        Object bridge=root(value("entryBridge",-1));if(bridge==null)return;
        Object organizer=organizerType.getConstructor().newInstance(),bt=field(bridge,"token");
        List<?> children=(List<?>)organizerType.getMethod("getChildTasks",token,int[].class).invoke(organizer,bt,null);
        Object tx=txType.getConstructor().newInstance();
        if(children!=null)for(Object child:children){
            if(number(child,"taskId")!=value("leasedTask",-1))throw new IllegalStateException("Unexpected task in entry container");
            txType.getMethod("reparent",token,token,boolean.class).invoke(tx,field(child,"token"),null,number(bridge,"displayId")!=0);
        }
        apply(tx);
        children=(List<?>)organizerType.getMethod("getChildTasks",token,int[].class).invoke(organizer,bt,null);
        if(children!=null&&!children.isEmpty())throw new IllegalStateException("Entry container is not empty");
        organizerType.getMethod("deleteRootTask",token).invoke(organizer,bt);state.remove("entryBridge");save();
    }
    static void moveExisting(Object task,int display,Object info)throws Exception{
        Object bridge=createBridge(display,1);state.setProperty("entryBridge",Integer.toString(number(bridge,"taskId")));save();
        Object tx=txType.getConstructor().newInstance(),tok=field(task,"token"),bt=field(bridge,"token");
        txType.getMethod("setWindowingMode",token,int.class).invoke(tx,tok,5);
        txType.getMethod("setBounds",token,rect).invoke(tx,tok,rect.getConstructor(int.class,int.class,int.class,int.class).newInstance(0,0,number(info,"logicalWidth"),number(info,"logicalHeight")));
        txType.getMethod("reparent",token,token,boolean.class).invoke(tx,tok,bt,true);
        txType.getMethod("reparent",token,token,boolean.class).invoke(tx,tok,null,true);
        apply(tx);cleanEntryBridge();
    }
    static void acquire(int display,String pkg,String component)throws Exception{
        if(display<=0||!pkg.matches("[A-Za-z0-9_]+(?:\\.[A-Za-z0-9_]+)+")||!component.startsWith(pkg+"/")||!component.matches("[A-Za-z0-9_.$/]+"))throw new IllegalArgumentException("Invalid app/display");
        Object info=displayInfo(display);if(info==null)throw new IllegalArgumentException("Display no longer exists");
        if(stateFile.exists())throw new IllegalStateException("Previous lease must be returned first");
        int user=(Integer)Class.forName("android.app.ActivityManager").getMethod("getCurrentUser").invoke(null);
        state.setProperty("boot",boot());state.setProperty("display",Integer.toString(display));state.setProperty("displayUnique",(String)field(info,"uniqueId"));state.setProperty("package",pkg);state.setProperty("user",Integer.toString(user));
        Object selected=null;
        for(Object t:roots()){
            if(number(t,"displayId")==0&&number(t,"numActivities")>0&&packageName(t).length()>0)remember(t);
            if(!matches(t,pkg,user)||number(t,"numActivities")==0)continue;
            if(number(t,"displayId")!=0)throw new IllegalStateException("Application already belongs to another display");
            if(mode(t)!=1&&mode(t)!=5)throw new IllegalStateException("Exit split-screen or picture-in-picture before casting");
            if(selected==null||priority(t)>priority(selected))selected=t;
        }
        // Check leaf tasks too: do not launch over an existing split-screen task.
        for(Object t:(List<?>)atm.getMethod("getTasks",int.class,boolean.class,boolean.class,int.class).invoke(service,200,false,false,-1)){
            if(matches(t,pkg,user)&&number(t,"numActivities")>0&&(selected==null||number(t,"taskId")!=number(selected,"taskId"))){
                if(number(t,"displayId")!=0)throw new IllegalStateException("Application already belongs to another display");
                if((Boolean)t.getClass().getMethod("hasParentTask").invoke(t))throw new IllegalStateException("Exit split-screen before casting this task");
            }
        }
        String kind="existing";
        if(selected==null){selected=recent(pkg,user);kind=selected==null?"new":"recent";}
        if(selected!=null){remember(selected);state.setProperty("leasedTask",Integer.toString(number(selected,"taskId")));}
        save(); // Persist ownership and original geometry before any task mutation.
        if(kind.equals("existing"))moveExisting(selected,display,info);
        else if(kind.equals("recent"))atm.getMethod("startActivityFromRecents",int.class,Class.forName("android.os.Bundle")).invoke(service,number(selected,"taskId"),options(display));
        else{
            Process p=new ProcessBuilder("am","start","-W","--display",Integer.toString(display),"--windowingMode","5","-a","android.intent.action.MAIN","-c","android.intent.category.LAUNCHER","-f","0x10000000","-n",component).redirectErrorStream(true).start();
            if(!p.waitFor(15,TimeUnit.SECONDS)){p.destroy();throw new IOException("Application launch timed out");}
            ByteArrayOutputStream bytes=new ByteArrayOutputStream();byte[] buffer=new byte[4096];int length;
            try(InputStream in=p.getInputStream()){while((length=in.read(buffer))!=-1)bytes.write(buffer,0,length);}
            String output=bytes.toString("UTF-8");
            if(p.exitValue()!=0||output.contains("Error:"))throw new IOException("Application launch failed: "+output);
        }
        Object acquired=null;
        for(int retry=0;retry<20&&acquired==null;retry++){
            for(Object t:roots())if(number(t,"displayId")==display&&matches(t,pkg,user)){acquired=t;break;}
            if(acquired==null)Thread.sleep(100);
        }
        if(acquired==null)throw new IllegalStateException("Task did not arrive on the virtual display");
        int id=number(acquired,"taskId");
        if(selected!=null&&id!=number(selected,"taskId"))throw new IllegalStateException("System did not preserve the selected task");
        state.setProperty("leasedTask",Integer.toString(id));save();
        System.out.println("ACQUIRED task="+id+" kind="+kind+" display="+display+" activities="+number(acquired,"numActivities"));
    }
    static void release(boolean recovery)throws Exception{
        if(!stateFile.exists()){System.out.println("RELEASED tasks=0");return;}
        try(FileInputStream in=new FileInputStream(stateFile)){state.load(in);}
        if(state.containsKey("boot")&&!state.getProperty("boot").equals(boot())){
            if(!stateFile.delete())throw new IOException("Cannot remove expired lease");
            System.out.println("RELEASED tasks=0 rebooted=true");return;
        }
        int display=value("display",-1),user=value("user",-1);
        if(display<=0||user<0)throw new IllegalArgumentException("Invalid lease state");
        boolean exists=sameDisplay();
        if(recovery&&exists){System.out.println("ACTIVE");return;}
        cleanEntryBridge();
        List<Object> tasks=new ArrayList<>();
        if(exists)for(Object t:roots())if(number(t,"displayId")==display&&number(t,"numActivities")>0){
            if(number(t,"userId")!=user)throw new IllegalStateException("Unexpected user on virtual display");
            tasks.add(t);
        }
        Object organizer=organizerType.getConstructor().newInstance(),bridge=root(value("bridge",-1));
        if(bridge!=null){
            List<?> children=(List<?>)organizerType.getMethod("getChildTasks",token,int[].class).invoke(organizer,field(bridge,"token"),null);
            if(children!=null)for(Object child:children){
                if(!Arrays.asList(state.getProperty("returning","").split(",")).contains(Integer.toString(number(child,"taskId"))))throw new IllegalStateException("Unexpected task in return container");
                tasks.add(child);
            }
        }
        if(!tasks.isEmpty()){
            StringJoiner ids=new StringJoiner(",");for(Object t:tasks)ids.add(Integer.toString(number(t,"taskId")));state.setProperty("returning",ids.toString());save();
            if(bridge==null){
                Object cookie=Class.forName("android.os.Binder").getConstructor().newInstance();
                organizerType.getMethod("createRootTask",int.class,int.class,Class.forName("android.os.IBinder"),boolean.class).invoke(organizer,0,1,cookie,false);
                for(Object t:roots())if(((List<?>)field(t,"launchCookies")).contains(cookie)){bridge=t;break;}
                if(bridge==null)throw new IllegalStateException("Cannot create background return container");
                state.setProperty("bridge",Integer.toString(number(bridge,"taskId")));save();
            }
            Object tx=txType.getConstructor().newInstance(),bridgeToken=field(bridge,"token");
            for(Object t:tasks){
                Object tok=field(t,"token");
                // An organizer-owned empty root is a temporary route to display 0.
                // Both reparents run in one WCT. The real task becomes a standalone root
                // at the bottom of display 0, without a foreground launch or HOME key.
                txType.getMethod("reparent",token,token,boolean.class).invoke(tx,tok,bridgeToken,false);
                txType.getMethod("reparent",token,token,boolean.class).invoke(tx,tok,null,false);
            }
            apply(tx);
            for(Object t:tasks){Object returned=root(number(t,"taskId"));if(returned==null||number(returned,"displayId")!=0)throw new IllegalStateException("Task return incomplete");}
            // Restore fullscreen only after the task is on the portrait phone display.
            // Doing so on the landscape virtual display creates a stale ColorOS letterbox
            // surface offset that survives a later portrait lease.
            Object restore=txType.getConstructor().newInstance();for(Object t:tasks)restoreConfig(restore,root(number(t,"taskId")));apply(restore);
        }
        if(bridge!=null){
            List<?> children=(List<?>)organizerType.getMethod("getChildTasks",token,int[].class).invoke(organizer,field(bridge,"token"),null);
            if(children!=null&&!children.isEmpty())throw new IllegalStateException("Return container is not empty");
            organizerType.getMethod("deleteRootTask",token).invoke(organizer,field(bridge,"token"));
        }
        if(!exists){
            // Disconnect fallback: scrcpy preserves tasks. Repair only our borrowed task's
            // geometry if still in the background. Never hide a task the user has reopened.
            Object t=root(value("leasedTask",-1));
            if(t!=null&&number(t,"displayId")==0&&matches(t,state.getProperty("package",""),user)&&!flag(t,"isVisible")){
                Object tx=txType.getConstructor().newInstance();restoreConfig(tx,t);apply(tx);
            }
        }
        if(exists)for(Object t:roots())if(number(t,"displayId")==display&&number(t,"numActivities")>0)throw new IllegalStateException("New activity appeared during task return; retry close");
        if(!stateFile.delete())throw new IOException("Cannot remove completed lease");
        System.out.println("RELEASED tasks="+tasks.size()+" background=true");
    }
    public static void main(String[] args){try{
        if(args.length<2||!args[1].matches("/data/local/tmp/appdeck-session-[a-f0-9]{32}\\.properties"))throw new IllegalArgumentException("Invalid session path");
        stateFile=new File(args[1]);state=new Properties();atm=Class.forName("android.app.IActivityTaskManager");service=Class.forName("android.app.ActivityTaskManager").getMethod("getService").invoke(null);
        token=Class.forName("android.window.WindowContainerToken");rect=Class.forName("android.graphics.Rect");txType=Class.forName("android.window.WindowContainerTransaction");organizerType=Class.forName("android.window.TaskOrganizer");
        if(args[0].equals("acquire")&&args.length==5)acquire(Integer.parseInt(args[2]),args[3],args[4]);
        else if(args[0].equals("release"))release(false);
        else if(args[0].equals("recover"))release(true);
        else throw new IllegalArgumentException("Unknown session command");
    }catch(Throwable e){while(e instanceof InvocationTargetException&&e.getCause()!=null)e=e.getCause();System.err.println("TASK_ERROR: "+e);System.exit(1);}}
}
