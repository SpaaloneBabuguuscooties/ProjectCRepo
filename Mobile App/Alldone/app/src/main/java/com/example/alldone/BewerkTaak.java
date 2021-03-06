package com.example.alldone;

import androidx.appcompat.app.AppCompatActivity;

import android.os.AsyncTask;
import android.content.Intent;
import android.os.Bundle;
import android.view.MenuItem;
import android.widget.TextView;
import android.widget.Toast;

import com.google.android.material.navigation.NavigationView;

import androidx.appcompat.app.ActionBarDrawerToggle;
import androidx.drawerlayout.widget.DrawerLayout;

import android.view.View;
import android.widget.Button;
import android.widget.EditText;

import org.apache.http.HttpEntity;
import org.apache.http.HttpResponse;
import org.apache.http.NameValuePair;
import org.apache.http.client.HttpClient;
import org.apache.http.client.entity.UrlEncodedFormEntity;
import org.apache.http.client.methods.HttpPost;
import org.apache.http.impl.client.DefaultHttpClient;
import org.apache.http.message.BasicNameValuePair;

import java.util.ArrayList;
import java.util.List;

import android.widget.ArrayAdapter;
import android.widget.Spinner;

public class BewerkTaak extends AppCompatActivity implements NavigationView.OnNavigationItemSelectedListener {
    private DrawerLayout mDrawerLayout;
    private ActionBarDrawerToggle mToggle;

    //String ServerURL = "http://145.137.123.23/alldone/v1/edit_data.php";
    //String ServerURL = "http://145.137.121.233/alldone/v1/edit_data.php";
    //String ServerURL = "http://145.137.121.231/alldone/v1/edit_data.php";
    //String ServerURL = "http://145.137.122.181/alldone/v1/edit_data.php";
    String ServerURL = "http://145.137.121.58/alldone/v1/edit_data.php";
    //String ServerURL = "http://192.168.188.62/alldone/v1/edit_data.php";

    // Getting the query to insert data, !IP address differs from location :)

    EditText titleEditText, descriptionEditText;
    String id, title, description, priority;
    Button button;
    String idString, titleString, descString, priorityString;
    Spinner prioritySpinner;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_bewerk_taak);

        mDrawerLayout = findViewById(R.id.drawerLayout); // Sidenav
        mToggle = new ActionBarDrawerToggle(this, mDrawerLayout, R.string.open, R.string.close); // Opening and closing the sidenav

        mDrawerLayout.addDrawerListener(mToggle);
        mToggle.syncState();

        Intent intent = getIntent();
        title = intent.getStringExtra("title");
        priority = intent.getStringExtra("priority");
        description = intent.getStringExtra("description");
        id = intent.getStringExtra("id");

        prioritySpinner = findViewById(R.id.spinner);
        ArrayAdapter<CharSequence> adapter = ArrayAdapter.createFromResource(this,
                R.array.priorities, android.R.layout.simple_spinner_item);
        adapter.setDropDownViewResource(android.R.layout.simple_spinner_dropdown_item);
        prioritySpinner.setAdapter(adapter); // Priority is chosen from a spinner which contains an array of possible options (0-3).

        getSupportActionBar().setDisplayHomeAsUpEnabled(true);
        NavigationView navigationView = findViewById(R.id.nv1);
        navigationView.setNavigationItemSelectedListener(this);

        titleEditText = findViewById(R.id.titleEditText);
        descriptionEditText = findViewById(R.id.descriptionEditText);

        titleEditText.setText(title, TextView.BufferType.EDITABLE);
        descriptionEditText.setText(description, TextView.BufferType.EDITABLE);

        button = findViewById(R.id.button);

        button.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View view) {

                GetData();
                if(titleString.matches("") || descString.matches("")) {
                    Toast.makeText(getApplicationContext(), "Please insert some data",Toast.LENGTH_SHORT).show();
                } // If the new task has no title and/or description, the data will not be inserted. Priority has default value 0 thus doesn't get compared.
                else {
                    InsertData(idString, titleString, descString, priorityString);
                } // Insert the data if nothing's empty.

            }
        });
    }

    public void GetData(){
        idString = id;
        titleString = titleEditText.getText().toString();
        descString = descriptionEditText.getText().toString();
        priorityString = prioritySpinner.getSelectedItem().toString(); // Converting the input in the edittexts and spinner to strings.
    }

    public void InsertData(final String id, final String title, final String description, final String priority){

        class SendPostReqAsyncTask extends AsyncTask<String, Void, String> {
            // Using AsyncTask to execute heavier tasks in the background on a dedicated thread
            // --> the app runs on a single thread, thus executing InsertData() which takes time to get a responsive could make the app non-responsive.

            @Override
            protected String doInBackground(String... params) {
                // The code is being executed in the background (thus doInBackground())

                String IdHolder = id;
                String TitleHolder = title;
                String DescriptionHolder = description;
                String PriorityHolder = priority;

                List<NameValuePair> nameValuePairs = new ArrayList<NameValuePair>(); // Making an list for the to be inserted data.

                nameValuePairs.add(new BasicNameValuePair("id", IdHolder));
                nameValuePairs.add(new BasicNameValuePair("title", TitleHolder));
                nameValuePairs.add(new BasicNameValuePair("description", DescriptionHolder));
                nameValuePairs.add(new BasicNameValuePair("priority", PriorityHolder));
                // Matching the values (chosen title, description & priority) with the keys from the table (title, description & priority).

                try { // Try so we can test for errors
                    HttpClient httpClient = new DefaultHttpClient();

                    HttpPost httpPost = new HttpPost(ServerURL);

                    httpPost.setEntity(new UrlEncodedFormEntity(nameValuePairs));

                    HttpResponse httpResponse = httpClient.execute(httpPost);

                    HttpEntity httpEntity = httpResponse.getEntity();
                }
                catch (Exception e) {
                    System.out.println("Exception was thrown");
                    e.printStackTrace();
                } // Error message if something went wrong

                return "Taak bewerkt";

            }

            @Override
            protected void onPostExecute(String result) {
                Toast.makeText(BewerkTaak.this, "Taak bewerkt!", Toast.LENGTH_LONG).show();
                Intent intent0 = new Intent(getApplicationContext(), Takenlijst.class);
                intent0.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                intent0.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TASK);
                startActivity(intent0);
            }
        }

        SendPostReqAsyncTask sendPostReqAsyncTask = new SendPostReqAsyncTask();

        sendPostReqAsyncTask.execute(id, title, description, priority);
    }

    @Override
    public boolean onOptionsItemSelected(MenuItem item) {

        if (mToggle.onOptionsItemSelected(item)) {
            return true;
        }

        return super.onOptionsItemSelected(item);
    }

    @Override
    public boolean onNavigationItemSelected(MenuItem menuItem) {
        switch (menuItem.getItemId()) {
            case (R.id.home):
                //Intent intent1 = new Intent(getApplicationContext(), MainActivity.class);
                //startActivity(intent1);
                break;
            case (R.id.new_task):
                Intent intent2 = new Intent(getApplicationContext(), MaakTaak.class);
                intent2.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                intent2.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TASK);
                startActivity(intent2);
            case (R.id.task_list):
                Intent intent3 = new Intent(getApplicationContext(), Takenlijst.class);
                startActivity(intent3);
                break;
            case (R.id.profile):
                Intent intent4 = new Intent(getApplicationContext(), Profiel.class);
                startActivity(intent4);
                break;
            case(R.id.log_out):
                Toast.makeText(getApplicationContext(), "Je bent uitgelogd",Toast.LENGTH_SHORT).show();
                Intent intent5 = new Intent(getApplicationContext(), MainActivity.class);
                intent5.addFlags(Intent.FLAG_ACTIVITY_NEW_TASK);
                intent5.addFlags(Intent.FLAG_ACTIVITY_CLEAR_TASK);
                startActivity(intent5);
        }
        return true;
    }
}
