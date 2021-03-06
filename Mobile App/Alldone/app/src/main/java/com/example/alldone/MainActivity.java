package com.example.alldone;

import android.content.Intent;
import android.os.Bundle;
import android.util.Log;
import android.view.View;
import android.widget.Button;
import android.widget.EditText;
import android.widget.Toast;

import org.json.JSONException;
import org.json.JSONObject;

import java.io.BufferedInputStream;
import java.io.BufferedReader;
import java.io.DataInputStream;
import java.io.DataOutput;
import java.io.DataOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.net.HttpURLConnection;
import java.net.MalformedURLException;
import java.net.URL;

import androidx.appcompat.app.AppCompatActivity;

public class MainActivity extends AppCompatActivity {
    String msg = "";
    private JSONObject json;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);

        final EditText username = (EditText)findViewById(R.id.username);
        final EditText password = (EditText)findViewById(R.id.password);
        final Button login = (Button)findViewById(R.id.buttonLogin);
        final Button regist = (Button)findViewById(R.id.buttonToRegist);

        regist.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                Intent toRegist = new Intent(getApplicationContext(), Register.class);
                startActivity(toRegist);
            }
        });

        login.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
               /* Intent intent0 = new Intent(getApplicationContext(), Takenlijst.class);
                intent0.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                intent0.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TASK);
                startActivity(intent0);*/

                JSONObject jObj = null;
                try {
                    jObj = new JSONObject()
                            .put("username", username.getText())
                            .put("password", password.getText());
                } catch (JSONException e) {
                    e.printStackTrace();
                }

                jObj.toString();

                final JSONObject finalJObj = jObj;
                Thread e = new Thread(new Runnable() {
                    @Override
                    public void run() {
                        Response response = Connection.Send("login", "POST", finalJObj.toString());
                        Connection.session = response.GetJSON().optString("sessionId");

                        response.PrettyPrint();
                        if(response.IsSuccessful()) {
                            msg = "Je bent ingelogd";
                            Intent intent0 = new Intent(getApplicationContext(), GroupTasklists.class);
                            startActivity(intent0);
                        }
                        else {
                            msg = "Er is iets fout gegaan";
                        }
                        MainActivity.this.runOnUiThread(new Runnable() {
                            @Override
                            public void run() {
                                Toast.makeText(getApplicationContext(), msg,Toast.LENGTH_SHORT).show();
                            }
                        });
                        //NetworkingShit(finalJObj);
                    }
                });
                e.start();
            }
        });
    }

}