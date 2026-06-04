FROM python:3.12-slim

WORKDIR /app

COPY packages/ /app/packages/
COPY backend-api/ /app/backend-api/

RUN pip install --no-cache-dir \
    -e /app/packages/shared \
    -e /app/packages/schema-engine \
    -e /app/packages/certificate-engine \
    -e /app/packages/verification-engine \
    && pip install --no-cache-dir -r /app/backend-api/requirements.txt

WORKDIR /app/backend-api
EXPOSE 8000
CMD ["sh", "-c", "uvicorn app.main:app --host 0.0.0.0 --port ${PORT:-8000}"]
