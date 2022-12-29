How to create service

Before talking about our main topic, for those of you who do not know what is a worker service, let's learn about what is worker service.
Worker Service is a built-in feature in .NET Core for creating background services. One example of using Worker Service is running periodical schedules like sending newsletter emails for clients every morning. To learn more about worker service, refer to this link.

We assume that you have created a Worker Service and now you want to deploy it on a Linux machine. First of all, as you learned in the previous article, we need to create a new service file, so use the below command to create a service file:

sudo nano /etc/systemd/system/appbackground.service

[Unit]
Description=Your description 

[Service]
Type=notify
WorkingDirectory=/home/centos/Desktop/services/

ExecStart=/usr/bin/dotnet /home/centos/Desktop/services/myapp.WorkerServic$


Environment=ASPNETCORE_ENVIRONMENT=Production
[Install]
WantedBy=multi-user.target



Then press ctrl+x to save its content and run the following commands:

sudo systemctl daemon-reload
sudo systemctl start appbackground.service


If you get an error after running sudo systemctl start appbackground.service you will need to add a small change to your worker service project.

Install Microsoft.Extensions.Hosting.Systemd by nuget:

dotnet add package Microsoft.Extensions.Hosting.Systemd


