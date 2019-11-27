package com.example.alldone;

import android.content.Intent;
import android.os.Bundle;
import android.view.MenuItem;
import android.widget.Toast;

import com.google.android.material.navigation.NavigationView;

import androidx.appcompat.app.ActionBarDrawerToggle;
import androidx.appcompat.app.AppCompatActivity;
import androidx.drawerlayout.widget.DrawerLayout;

public class MaakTaak extends AppCompatActivity implements NavigationView.OnNavigationItemSelectedListener {
    private DrawerLayout mDrawerLayout;
    private ActionBarDrawerToggle mToggle;

    private String userData;

    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_maak_taak);

        Intent intent=getIntent();

        userData = intent.getStringExtra("userdata");

        mDrawerLayout = (DrawerLayout) findViewById(R.id.drawerLayout);
        mToggle = new ActionBarDrawerToggle(this, mDrawerLayout, R.string.open, R.string.close);

        mDrawerLayout.addDrawerListener(mToggle);
        mToggle.syncState();

        getSupportActionBar().setDisplayHomeAsUpEnabled(true);
        NavigationView navigationView = (NavigationView) findViewById(R.id.nv1);
        navigationView.setNavigationItemSelectedListener(this);
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
                //Intent intent2 = new Intent(getApplicationContext(), MaakTaak.class);
                //startActivity(intent2);
                break;
            case (R.id.task_list):
                Intent intent3 = new Intent(getApplicationContext(), Takenlijst.class);
                intent3.putExtra("userdata", userData);
                startActivity(intent3);
                break;
            case (R.id.profile):
                Intent intent4 = new Intent(getApplicationContext(), Profiel.class);
                intent4.putExtra("userdata", userData);
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
