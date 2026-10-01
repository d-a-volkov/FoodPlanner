FROM python:3.11-slim

WORKDIR /app

ENV PYTHONUNBUFFERED=1 \
    KUPER_STATE=/data/kuper_state.json \
    KUPER_IMPERSONATE=chrome131

COPY requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY app.py matcher.py ./

EXPOSE 8082

HEALTHCHECK --interval=60s --timeout=5s --start-period=15s \
    CMD python -c "import urllib.request,sys; urllib.request.urlopen('http://127.0.0.1:8082/health', timeout=4)" || exit 1

CMD ["uvicorn", "app:app", "--host", "0.0.0.0", "--port", "8082"]