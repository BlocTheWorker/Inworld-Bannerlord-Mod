import * as dotenv from 'dotenv'
dotenv.config()
import axios from 'axios';

const INWORLD_FIREBASE_ID = "AIzaSyAPVBLVid0xPwjuU4Gmn_6_GyqxBq-SwQs"
const LOGIN_URI = "https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=" + INWORLD_FIREBASE_ID
const GOOGLE_TOKEN_URI = "https://securetoken.googleapis.com/v1/token?key=" + INWORLD_FIREBASE_ID

// This class represents the Inworld Login System which provides functionality to 
// log in to the Inworld Portal and maintain an inworld token. It allows users to 
// securely authenticate and access various features of the Inworld platform. The 
// inworld token is stored and managed by this class, ensuring a seamless user 
// experience across different pages and sessions.
export default class InworldLoginSystem {

    isDateInThePast(date) {
        const today = new Date();
        return date < today;
    }

    async Login(){
        let loginData = {
            "returnSecureToken": true,
            "email": process.env.LOGIN_EMAIL,
            "password": process.env.LOGIN_PASSWORD
        }
        let loginResponse = await axios.post(LOGIN_URI, loginData)
        this.refreshToken = loginResponse.data.refreshToken;
    }

    async LoginGoogleApis(){
        if(this.refreshToken == null)
            await this.Login()
        let formData = new URLSearchParams()
        formData.append("grant_type", "refresh_token")
        formData.append("refresh_token", this.refreshToken)
        let tokenResponse = await axios.post(GOOGLE_TOKEN_URI, formData);
        this.accessToken = tokenResponse.data.access_token
        let d = new Date();
        d.setSeconds(d.getSeconds() + tokenResponse.data.expiresIn);
        this.expiresIn = d;
        return this.accessToken
    }

    async RefreshToken(){
        this.refreshToken = null;
        return await this.LoginGoogleApis();
    }

    async GetPortalToken(){
        if(this.accessToken == null || this.expiresIn == null || this.isDateInThePast(this.expiresIn)){
            console.log("Token expired or doesnt exist", this.accessToken, this.expiresIn)
            return await this.LoginGoogleApis()
        } else {
            return this.accessToken;
        }
    }
}