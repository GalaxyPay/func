const FUNC = {
  get api() {
    return axios.create({ baseURL: `http://${location.hostname}:3536` });
  },
};

export default FUNC;
