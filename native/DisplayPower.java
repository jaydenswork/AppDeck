import java.lang.reflect.*;

public final class DisplayPower {
    static Object info(int id)throws Exception{
        Class<?> global=Class.forName("android.hardware.display.DisplayManagerGlobal");
        return global.getMethod("getDisplayInfo",int.class).invoke(global.getMethod("getInstance").invoke(null),id);
    }
    static int state(Object info)throws Exception{return info==null?-1:info.getClass().getField("state").getInt(info);}
    public static void main(String[] args){try{
        if(args.length!=1)throw new IllegalArgumentException("Virtual display id required");
        int id=Integer.parseInt(args[0]);if(id<=0)throw new IllegalArgumentException("Main display is not allowed");
        Object display=info(id);if(display==null)throw new IllegalStateException("Virtual display no longer exists");
        if(display.getClass().getField("type").getInt(display)!=5||!"scrcpy".equals(display.getClass().getField("name").get(display))||!((String)display.getClass().getField("uniqueId").get(display)).startsWith("virtual:com.android.shell,"))throw new IllegalArgumentException("Not an AppDeck virtual display");
        int before=state(display),mainBefore=state(info(0));boolean changed=false;
        if(before!=2){
            Class<?> binder=Class.forName("android.os.IBinder"),manager=Class.forName("android.hardware.display.IDisplayManager");
            Object raw=Class.forName("android.os.ServiceManager").getMethod("getService",String.class).invoke(null,"display");
            Object service=Class.forName("android.hardware.display.IDisplayManager$Stub").getMethod("asInterface",binder).invoke(null,raw);
            Object result;
            try{result=manager.getMethod("requestDisplayPower",int.class,int.class).invoke(service,id,2);}
            catch(NoSuchMethodException e){result=manager.getMethod("requestDisplayPower",int.class,boolean.class).invoke(service,id,true);}
            if(Boolean.FALSE.equals(result))throw new IllegalStateException("Virtual display power request rejected");
            changed=true;
            for(int retry=0;retry<15&&state(info(id))!=2;retry++)Thread.sleep(100);
        }
        System.out.println("display="+id+" before="+before+" after="+state(info(id))+" changed="+changed+" mainBefore="+mainBefore+" mainAfter="+state(info(0)));
        if(state(info(id))!=2)throw new IllegalStateException("Virtual display did not become active");
    }catch(Throwable e){while(e instanceof InvocationTargetException&&e.getCause()!=null)e=e.getCause();System.err.println("DISPLAY_POWER_ERROR: "+e);System.exit(1);}}
}
