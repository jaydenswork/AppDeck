import java.lang.reflect.*;
import java.util.List;
import java.util.ArrayList;

/** Operates only on tasks currently on one AppDeck virtual display. */
public final class DisplayLayout {
    public static void main(String[] args) throws Exception {
        int display=Integer.parseInt(args[0]), width=Integer.parseInt(args[1]),height=Integer.parseInt(args[2]);
        if(display<=0||width<=0||height<=0)throw new IllegalArgumentException("Virtual display and positive size required");
        Object service=Class.forName("android.app.ActivityTaskManager").getMethod("getService").invoke(null);
        Method list=Class.forName("android.app.IActivityTaskManager").getMethod("getAllRootTaskInfos");
        Class<?> tokenType=Class.forName("android.window.WindowContainerToken"),rectType=Class.forName("android.graphics.Rect"),txType=Class.forName("android.window.WindowContainerTransaction");
        Object tx=txType.getConstructor().newInstance();int count=0,total=0;List<Integer> changed=new ArrayList<>();
        for(Object task:(List<?>)list.invoke(service)){
            Class<?> type=task.getClass();
            if(type.getField("displayId").getInt(task)!=display)continue;
            total++;
            Object configuration=type.getField("configuration").get(task);
            Object wc=configuration.getClass().getField("windowConfiguration").get(configuration);
            Object bounds=wc.getClass().getMethod("getBounds").invoke(wc);
            int mode=(Integer)wc.getClass().getMethod("getWindowingMode").invoke(wc);
            int left=rectType.getField("left").getInt(bounds),top=rectType.getField("top").getInt(bounds);
            int right=rectType.getField("right").getInt(bounds),bottom=rectType.getField("bottom").getInt(bounds);
            if(mode==5&&left==0&&top==0&&right==width&&bottom==height&&args.length<4)continue;
            Object token=type.getField("token").get(task),rect=rectType.getConstructor(int.class,int.class,int.class,int.class).newInstance(0,0,width,height);
            // am start --windowingMode is only a launch hint and is ignored for reused tasks.
            // Apply mode to the actual task, including previously fullscreen tasks.
            txType.getMethod("setWindowingMode",tokenType,int.class).invoke(tx,token,5);
            changed.add(type.getField("taskId").getInt(task));
            count++;
        }
        if(count>0){
            Class<?> organizer=Class.forName("android.window.WindowOrganizer");organizer.getMethod("applyTransaction",txType).invoke(organizer.getConstructor().newInstance(),tx);
            // ColorOS can retain the app's previous surface after a windowing-mode change.
            // A real bounds change through resizeTask dispatches the missing Activity resize;
            // WCT.setBounds or RESIZE_MODE_FORCED with identical bounds is insufficient.
            Method resize=Class.forName("android.app.IActivityTaskManager").getMethod("resizeTask",int.class,rectType,int.class);
            for(Object current:(List<?>)list.invoke(service)){
                Class<?> type=current.getClass();int id=type.getField("taskId").getInt(current);
                if(type.getField("displayId").getInt(current)==display&&changed.contains(id)){
                    resize.invoke(service,id,rectType.getConstructor(int.class,int.class,int.class,int.class).newInstance(0,0,Math.max(1,width-2),height),0);
                    resize.invoke(service,id,rectType.getConstructor(int.class,int.class,int.class,int.class).newInstance(0,0,width,height),0);
                }
            }
        }
        System.out.println("display="+display+" tasks="+total+" changed="+count+" target="+width+"x"+height);
    }
}
