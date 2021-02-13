from multiprocessing import Process
from flask import Flask
from flask import request
import time
import requests

app = Flask(__name__)

@app.route('/api', methods=['POST'])
def request_api():
   content = request.json
   callbackUrl = content['externalTriggerCallbackUrl']
   p1 = Process(target=doing_work, args=(callbackUrl,))
   p1.start()
   return 'OK'

def doing_work(callbackUrl):
   print('working...')
   print(callbackUrl)
   time.sleep(5)
   res = requests.post(callbackUrl, json={'blobUri': 'jim hopper', 'success': True})
   print(res)
   print('work done...')


if __name__ == '__main__':
   app.run(host ='0.0.0.0', port = 5001, debug = True)