import java.lang.reflect.*;
import java.util.List;

/** AppDeck: clear only the bounds/mode overrides of tasks on a specified virtual display. */
public final class ResetDisplay {
    public static void main(String[] args) throws Exception {
        int display=Integer.parseInt(args[0]);
        if(display<=0)throw new IllegalArgumentException("A virtual display ID is required");
        reset(display,-1);
    }
    static void reset(int display,int onlyTask) throws Exception {
        Object service=Class.forName("android.app.ActivityTaskManager").getMethod("getService").invoke(null);
        Method list=Class.forName("android.app.IActivityTaskManager").getMethod("getAllRootTaskInfos");
        Class<?> tokenType=Class.forName("android.window.WindowContainerToken");
        Class<?> rectType=Class.forName("android.graphics.Rect");
        Class<?> txType=Class.forName("android.window.WindowContainerTransaction");
        Object tx=txType.getConstructor().newInstance();int count=0;
        for(Object task:(List<?>)list.invoke(service)){
            Class<?> type=task.getClass();
            int id=type.getField("taskId").getInt(task);
            if(type.getField("displayId").getInt(task)!=display||(onlyTask>=0&&id!=onlyTask))continue;
            Object token=type.getField("token").get(task);
            txType.getMethod("setWindowingMode",tokenType,int.class).invoke(tx,token,1);
            txType.getMethod("setBounds",tokenType,rectType).invoke(tx,token,null);
            count++;
        }
        Class<?> organizer=Class.forName("android.window.WindowOrganizer");
        organizer.getMethod("applyTransaction",txType).invoke(organizer.getConstructor().newInstance(),tx);
        System.out.println("Reset "+count+" tasks on display "+display);
    }
}
