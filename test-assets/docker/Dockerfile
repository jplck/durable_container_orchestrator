FROM python:3.8

WORKDIR /code

COPY src/requirements.txt .
RUN pip install -r requirements.txt

COPY src/ .

EXPOSE 5001

# command to run on container start
CMD [ "python", "./app.py" ]