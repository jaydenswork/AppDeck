import android.content.Context;
import android.content.pm.PackageManager;
import android.graphics.Bitmap;
import android.graphics.Canvas;
import android.graphics.drawable.Drawable;
import android.os.Looper;
import android.util.Base64;
import java.io.ByteArrayOutputStream;

/** Reads public application icon resources; never launches or changes an application. */
public final class AppIcons {
    public static void main(String[] packages) throws Exception {
        if (Looper.myLooper() == null) Looper.prepareMainLooper();
        Class<?> thread = Class.forName("android.app.ActivityThread");
        Object activityThread = thread.getMethod("systemMain").invoke(null);
        Context context = (Context) thread.getMethod("getSystemContext").invoke(activityThread);
        context = context.createPackageContext("com.android.shell", Context.CONTEXT_IGNORE_SECURITY);
        PackageManager manager = context.getPackageManager();
        for (String name : packages) {
            if (!name.matches("[A-Za-z0-9_]+(?:\\.[A-Za-z0-9_]+)+")) continue;
            try {
                Drawable drawable = manager.getApplicationIcon(name);
                Bitmap bitmap = Bitmap.createBitmap(96, 96, Bitmap.Config.ARGB_8888);
                drawable.setBounds(0, 0, 96, 96);
                drawable.draw(new Canvas(bitmap));
                ByteArrayOutputStream output = new ByteArrayOutputStream();
                bitmap.compress(Bitmap.CompressFormat.PNG, 100, output);
                bitmap.recycle();
                System.out.println("ICON\t" + name + "\t" + Base64.encodeToString(output.toByteArray(), Base64.NO_WRAP));
            } catch (Exception error) {
                System.err.println("ICON_UNAVAILABLE " + name + " " + error);
            }
        }
    }
}
