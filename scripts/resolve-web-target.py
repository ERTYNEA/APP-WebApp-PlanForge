"""Validate the existing Web publishing target entirely inside the trusted runner."""
import base64
import os
import urllib.error
import urllib.parse
import urllib.request
import xml.etree.ElementTree as ET


class PublishingTarget:
	def __init__(self, public_url, scm_url, username, password):
		self.public_url=public_url
		self.scm_url=scm_url
		self.username=username
		self.password=password

	def __repr__(self):
		return f"PublishingTarget(public_url={self.public_url!r}, credentials=hidden)"


def parse_profile(xml, expected_name):
	try:
		profiles=list(ET.fromstring(xml).iter("publishProfile"))
	except ET.ParseError:
		raise ValueError("The existing Web publish profile is not valid XML.") from None
	profile=next((item for item in profiles if item.get("publishMethod")=="MSDeploy"),None)
	if profile is None:
		raise ValueError("The existing Web publish profile needs an MSDeploy entry.")
	username=profile.get("userName","")
	password=profile.get("userPWD","")
	if any(not value.strip() or value.strip().casefold()=="redacted" for value in [username,password]):
		raise ValueError("The existing Web publishing credential is missing, blank or REDACTED. Owner must refresh only the existing publish-profile secret.")
	def host(value):
		parsed=urllib.parse.urlparse(value if "://" in value else "https://"+value)
		if parsed.scheme!="https" or parsed.username or parsed.password or parsed.port not in [None,443]:
			raise ValueError("Only the existing HTTPS Azure target is allowed.")
		return (parsed.hostname or "").lower()
	scm=host(profile.get("publishUrl",""))
	public=host(profile.get("destinationAppUrl") or scm.replace(".scm.","."))
	expected=expected_name.lower()
	if not public.endswith(".azurewebsites.net") or not (public.startswith(expected+"-") or public==expected+".azurewebsites.net") or ".scm." not in scm or scm.replace(".scm.",".")!=public:
		raise ValueError("Publishing profile targets do not match the existing expected Web app.")
	return PublishingTarget("https://"+public,"https://"+scm,username,password)


class NoRedirect(urllib.request.HTTPRedirectHandler):
	def redirect_request(self, req, fp, code, msg, headers, newurl):
		return None


if __name__ == "__main__":
	target=parse_profile(os.environ["PUBLISH_PROFILE"],"PlanForge-Web")
	print("Profile credential fields valid:",True)
	print("Public Web target:",target.public_url)
	authorization=base64.b64encode((target.username+":"+target.password).encode()).decode()
	request=urllib.request.Request(target.scm_url+"/api/settings",headers={"Authorization":"Basic "+authorization})
	try:
		with urllib.request.build_opener(NoRedirect()).open(request,timeout=30) as response:
			print("Web SCM credential preflight status:",response.status)
			# Do not read, print or save settings bodies or credential values.
	except urllib.error.HTTPError as error:
		print("Web SCM credential preflight status:",error.code)
		raise RuntimeError("Existing Web publishing credential was rejected; no Web deployment was attempted.") from None
	with open(os.environ["GITHUB_OUTPUT"],"a",encoding="utf-8") as output:
		output.write("base-url="+target.public_url+"\n")
