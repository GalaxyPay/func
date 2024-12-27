const FUNC = {
  get api() {
    return axios.create({ baseURL: "http://localhost:3536" });
  },
};

export default FUNC;
