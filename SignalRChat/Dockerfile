# 3.0.0-preview-aspnetcore-runtime
From microsoft/dotnet:3.0.100-preview-sdk

RUN mkdir /appsrc  
# WORKDIR /appsrc

RUN echo $(apt-cache search libssl)

# RUN apt-get install libssl1.0.2
# RUN apt-get install libssl1.0.2
RUN apt-get update
RUN apt-get install libssl1.0.2 libcurl3 libasound2 wget -y


COPY . /appsrc

WORKDIR /appsrc
RUN echo $(ls)
RUN dotnet restore
RUN dotnet build

EXPOSE 5001
EXPOSE 5000

CMD ["dotnet","run"]
