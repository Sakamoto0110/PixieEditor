# Extraction origin

PixieEditor was extracted from PixieEngine at
`523cfa75312231e827e19051e546de7bfbc55bb4` under PIXIE-PROPOSAL-022.
The transfer also includes the GLFW build integration prepared after that commit.
PixieEngine retains its history, native ABI binding producer and native write probe.
The installed Engine SDK carries the build bindings. The Editor owns its source,
application version, test harnesses and separately built GLFW 3.5.1 artifact.
