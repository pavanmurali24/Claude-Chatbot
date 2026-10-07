"""Streamlit chat UI for the .NET 10 chatbot backend.

Run with:  streamlit run app.py
The backend must already be listening (default http://localhost:5179).
"""

import os

import requests
import streamlit as st

DEFAULT_BACKEND = os.environ.get("CHATBOT_API", "http://localhost:5179")
REQUEST_TIMEOUT = 60

st.set_page_config(page_title="Chatbot sample", page_icon="💬")


def stream_reply(backend: str, messages: list[dict]):
    """Yield reply chunks from the backend's server-sent-events endpoint."""
    with requests.post(
        f"{backend}/api/chat/stream",
        json={"messages": messages},
        headers={"Accept": "text/event-stream"},
        stream=True,
        timeout=REQUEST_TIMEOUT,
    ) as response:
        response.raise_for_status()
        for raw_line in response.iter_lines(decode_unicode=True):
            # Blank lines separate events; "event:" lines carry the type, which we don't need.
            if not raw_line or not raw_line.startswith("data:"):
                continue
            chunk = raw_line[len("data:") :]
            # SSE readers strip exactly one space after the colon; the rest is payload.
            if chunk.startswith(" "):
                chunk = chunk[1:]
            yield chunk


def fetch_reply(backend: str, messages: list[dict]) -> str:
    """Get the whole reply in one shot from the non-streaming endpoint."""
    response = requests.post(
        f"{backend}/api/chat",
        json={"messages": messages},
        timeout=REQUEST_TIMEOUT,
    )
    response.raise_for_status()
    return response.json()["reply"]


with st.sidebar:
    st.header("Settings")
    backend_url = st.text_input("Backend URL", value=DEFAULT_BACKEND).rstrip("/")
    use_streaming = st.toggle("Stream the reply", value=True)

    if st.button("Check backend", use_container_width=True):
        try:
            health = requests.get(f"{backend_url}/health", timeout=5).json()
            st.success(f"Online · model `{health['model']}`")
        except requests.RequestException as error:
            st.error(f"Unreachable: {error}")

    if st.button("Clear chat", use_container_width=True):
        st.session_state.messages = []
        st.rerun()

    st.caption("Replies come from a rule-based mock engine in the .NET backend — no model calls, no API key.")

st.title("💬 Chatbot sample")
st.caption("Streamlit frontend · ASP.NET Core minimal API on .NET 10 · mock chat engine")

if "messages" not in st.session_state:
    st.session_state.messages = []

for message in st.session_state.messages:
    with st.chat_message(message["role"]):
        st.markdown(message["content"])

if prompt := st.chat_input("Say something..."):
    st.session_state.messages.append({"role": "user", "content": prompt})
    with st.chat_message("user"):
        st.markdown(prompt)

    with st.chat_message("assistant"):
        try:
            if use_streaming:
                reply = st.write_stream(stream_reply(backend_url, st.session_state.messages))
            else:
                reply = fetch_reply(backend_url, st.session_state.messages)
                st.markdown(reply)
        except requests.RequestException as error:
            reply = None
            st.error(
                f"Could not reach the backend at {backend_url}.\n\n"
                f"Start it with `dotnet run` in `backend/ChatbotApi`, then try again.\n\n"
                f"Details: {error}"
            )

    if reply:
        st.session_state.messages.append({"role": "assistant", "content": reply})
