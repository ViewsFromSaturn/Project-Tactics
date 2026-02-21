"""
Project Tactics - Flask Backend
REST API + SocketIO for authentication, character CRUD, game data, and real-time messaging.
"""
import os
from flask import Flask
from flask_cors import CORS
from dotenv import load_dotenv

from database import db
from routes.auth import auth_bp
from routes.characters import characters_bp
from routes.admin import admin_bp
from socketio_server import socketio

load_dotenv()


def create_app():
    app = Flask(__name__)

    # ─── CONFIGURATION ───────────────────────────────────────
    app.config["SECRET_KEY"] = os.getenv("SECRET_KEY", "dev-secret-change-me")
    app.config["SQLALCHEMY_DATABASE_URI"] = os.getenv(
        "DATABASE_URL", "postgresql://tactics:tactics@localhost:5432/project_tactics"
    )
    app.config["SQLALCHEMY_TRACK_MODIFICATIONS"] = False
    app.config["MAX_CONTENT_LENGTH"] = 2 * 1024 * 1024  # 2MB max upload

    # ─── EXTENSIONS ──────────────────────────────────────────
    db.init_app(app)
    CORS(app, supports_credentials=True)
    socketio.init_app(app)

    # ─── BLUEPRINTS ──────────────────────────────────────────
    app.register_blueprint(auth_bp, url_prefix="/api/auth")
    app.register_blueprint(characters_bp, url_prefix="/api/characters")
    app.register_blueprint(admin_bp, url_prefix="/api/admin")

    # ─── HEALTH CHECK ────────────────────────────────────────
    @app.route("/api/health")
    def health():
        return {"status": "ok", "game": "Project Tactics", "version": "1.0"}

    # ─── CREATE TABLES ───────────────────────────────────────
    with app.app_context():
        db.create_all()

    return app


if __name__ == "__main__":
    app = create_app()
    port = int(os.getenv("PORT", 5000))
    # Use socketio.run instead of app.run for WebSocket support
    socketio.run(app, host="0.0.0.0", port=port, debug=True, allow_unsafe_werkzeug=True)
